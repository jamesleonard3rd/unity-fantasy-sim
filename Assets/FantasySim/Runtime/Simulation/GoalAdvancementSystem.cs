using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation.State;
using UnityEngine;
using EntityId = FantasySim.Core.Ids.EntityId;

namespace FantasySim.Simulation
{
    public sealed class GoalAdvancementSystem
    {
        public const string StepCompletedEventType = "goal.step_completed";
        public const string GoalCompletedEventType = "goal.completed";
        public const int EventSignificance = 2;

        public const string WaitUntilStepType = "wait_until";

        private static readonly HashSet<string> DeferredStepTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "travel_to_region",
            "register_for_tournament",
            "compete_in_tournament"
        };

        private readonly IReadOnlyDictionary<string, GoalProgressionDefinition> progressions;

        public GoalAdvancementSystem(IReadOnlyDictionary<string, GoalProgressionDefinition> progressions = null)
        {
            this.progressions = progressions ?? new Dictionary<string, GoalProgressionDefinition>(StringComparer.Ordinal);
        }

        public IReadOnlyList<RegionStateDelta> Tick(LoadedPartitionState state, GameTick tick)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Dictionary<long, RegionId> regionByEntityId = state.Entities.ToDictionary(
                entity => entity.Id.Value,
                entity => entity.RegionId
            );

            var deltasByRegion = new Dictionary<long, MutableAdvancementDelta>();
            foreach (EntityGoalState goal in state.EntityGoals.OrderBy(goal => goal.Id))
            {
                if (!string.Equals(goal.State, GoalScoringSystem.ActiveState, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!regionByEntityId.TryGetValue(goal.EntityId.Value, out RegionId regionId))
                {
                    continue;
                }

                if (!progressions.TryGetValue(goal.GoalKey, out GoalProgressionDefinition progression) || !progression.HasSteps)
                {
                    continue;
                }

                AdvancementResult result = Advance(goal, progression, regionId, tick);
                if (result == null)
                {
                    continue;
                }

                MutableAdvancementDelta delta = GetOrCreateDelta(deltasByRegion, regionId, tick);
                delta.UpdatedEntityGoals.Add(result.UpdatedGoal);
                delta.Events.AddRange(result.Events);
            }

            return deltasByRegion.Values
                .OrderBy(delta => delta.RegionId.Value)
                .Select(delta => delta.ToRegionStateDelta())
                .ToList();
        }

        private AdvancementResult Advance(EntityGoalState goal, GoalProgressionDefinition progression, RegionId regionId, GameTick tick)
        {
            GoalProgressPayload payload = ParsePayload(goal.PayloadJson);
            int stepIndex = Math.Max(0, payload.currentStepIndex);
            if (stepIndex >= progression.StepTypes.Count)
            {
                return null;
            }

            string stepType = progression.StepTypes[stepIndex];
            if (!TryCompleteStep(stepType, payload, tick))
            {
                return null;
            }

            int nextStepIndex = stepIndex + 1;
            payload.currentStepIndex = nextStepIndex;

            var events = new List<PendingWorldEvent>
            {
                CreateStepCompletedEvent(goal, regionId, stepType, stepIndex, tick)
            };

            bool goalComplete = nextStepIndex >= progression.StepTypes.Count;
            if (goalComplete)
            {
                events.Add(CreateGoalCompletedEvent(goal, regionId, tick));
            }

            var updatedGoal = new EntityGoalState(
                goal.Id,
                goal.EntityId,
                goal.GoalKey,
                goal.Priority,
                goal.State,
                SerializePayload(payload)
            );

            return new AdvancementResult(updatedGoal, events);
        }

        private static bool TryCompleteStep(string stepType, GoalProgressPayload payload, GameTick tick)
        {
            if (string.Equals(stepType, WaitUntilStepType, StringComparison.Ordinal))
            {
                if (payload.targetGameTick < 0)
                {
                    return false;
                }

                return tick.Value >= payload.targetGameTick;
            }

            // Deferred step types (travel, tournament registration/competition) have no handler yet, so the
            // goal stalls on them until the travel and world-process layers exist.
            return false;
        }

        private static MutableAdvancementDelta GetOrCreateDelta(
            Dictionary<long, MutableAdvancementDelta> deltasByRegion,
            RegionId regionId,
            GameTick tick
        )
        {
            if (!deltasByRegion.TryGetValue(regionId.Value, out MutableAdvancementDelta delta))
            {
                delta = new MutableAdvancementDelta(regionId, tick);
                deltasByRegion.Add(regionId.Value, delta);
            }

            return delta;
        }

        private static GoalProgressPayload ParsePayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return new GoalProgressPayload();
            }

            try
            {
                return JsonUtility.FromJson<GoalProgressPayload>(payloadJson) ?? new GoalProgressPayload();
            }
            catch (ArgumentException)
            {
                return new GoalProgressPayload();
            }
        }

        private static string SerializePayload(GoalProgressPayload payload)
        {
            return JsonUtility.ToJson(payload);
        }

        private static PendingWorldEvent CreateStepCompletedEvent(
            EntityGoalState goal,
            RegionId regionId,
            string stepType,
            int stepIndex,
            GameTick tick
        )
        {
            string payloadJson = string.Format(
                "{{\"goalKey\":\"{0}\",\"stepType\":\"{1}\",\"stepIndex\":{2}}}",
                goal.GoalKey,
                stepType,
                stepIndex
            );
            return new PendingWorldEvent(
                regionId,
                StepCompletedEventType,
                EventSignificance,
                payloadJson,
                tick,
                goal.EntityId
            );
        }

        private static PendingWorldEvent CreateGoalCompletedEvent(EntityGoalState goal, RegionId regionId, GameTick tick)
        {
            string payloadJson = string.Format("{{\"goalKey\":\"{0}\"}}", goal.GoalKey);
            return new PendingWorldEvent(
                regionId,
                GoalCompletedEventType,
                EventSignificance,
                payloadJson,
                tick,
                goal.EntityId
            );
        }

        [Serializable]
        private sealed class GoalProgressPayload
        {
            public int currentStepIndex = 0;
            public long targetGameTick = -1;
        }

        private sealed class AdvancementResult
        {
            public AdvancementResult(EntityGoalState updatedGoal, IReadOnlyList<PendingWorldEvent> events)
            {
                UpdatedGoal = updatedGoal;
                Events = events;
            }

            public EntityGoalState UpdatedGoal { get; }
            public IReadOnlyList<PendingWorldEvent> Events { get; }
        }

        private sealed class MutableAdvancementDelta
        {
            public MutableAdvancementDelta(RegionId regionId, GameTick tick)
            {
                RegionId = regionId;
                Tick = tick;
            }

            public RegionId RegionId { get; }
            public GameTick Tick { get; }
            public List<PendingWorldEvent> Events { get; } = new List<PendingWorldEvent>();
            public List<EntityGoalState> UpdatedEntityGoals { get; } = new List<EntityGoalState>();

            public RegionStateDelta ToRegionStateDelta()
            {
                return new RegionStateDelta(
                    RegionId,
                    Tick,
                    Events,
                    null,
                    null,
                    null,
                    UpdatedEntityGoals,
                    null
                );
            }
        }
    }
}
