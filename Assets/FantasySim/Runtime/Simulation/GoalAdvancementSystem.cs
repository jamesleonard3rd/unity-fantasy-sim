using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation.State;
using FantasySim.Simulation.Travel;
using UnityEngine;
using EntityId = FantasySim.Core.Ids.EntityId;

namespace FantasySim.Simulation
{
    public sealed class GoalAdvancementSystem
    {
        public const string StepCompletedEventType = "goal.step_completed";
        public const string GoalCompletedEventType = "goal.completed";
        public const string RegionEnteredEventType = "travel.region_entered";
        public const int EventSignificance = 2;

        public const string WaitUntilStepType = "wait_until";
        public const string TravelStepType = "travel_to_region";

        private static readonly HashSet<string> DeferredStepTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "register_for_tournament",
            "compete_in_tournament"
        };

        private readonly IReadOnlyDictionary<string, GoalProgressionDefinition> progressions;
        private readonly TravelGraph travelGraph;

        public GoalAdvancementSystem(
            IReadOnlyDictionary<string, GoalProgressionDefinition> progressions = null,
            TravelGraph travelGraph = null
        )
        {
            this.progressions = progressions ?? new Dictionary<string, GoalProgressionDefinition>(StringComparer.Ordinal);
            this.travelGraph = travelGraph;
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

            Dictionary<long, string> keyByRegionId = new Dictionary<long, string>();
            foreach (RegionState region in state.Regions)
            {
                keyByRegionId[region.Id.Value] = region.Key;
            }

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

                keyByRegionId.TryGetValue(regionId.Value, out string currentRegionKey);

                AdvancementResult result = Advance(goal, progression, regionId, currentRegionKey, tick);
                if (result == null)
                {
                    continue;
                }

                MutableAdvancementDelta delta = GetOrCreateDelta(deltasByRegion, regionId, tick);
                if (result.UpdatedGoal != null)
                {
                    delta.UpdatedEntityGoals.Add(result.UpdatedGoal);
                }

                delta.Events.AddRange(result.Events);
                delta.EntityMoves.AddRange(result.EntityMoves);
            }

            return deltasByRegion.Values
                .OrderBy(delta => delta.RegionId.Value)
                .Select(delta => delta.ToRegionStateDelta())
                .ToList();
        }

        private AdvancementResult Advance(
            EntityGoalState goal,
            GoalProgressionDefinition progression,
            RegionId regionId,
            string currentRegionKey,
            GameTick tick
        )
        {
            GoalProgressPayload payload = ParsePayload(goal.PayloadJson);
            int stepIndex = Math.Max(0, payload.currentStepIndex);
            if (stepIndex >= progression.StepTypes.Count)
            {
                return null;
            }

            string stepType = progression.StepTypes[stepIndex];

            var events = new List<PendingWorldEvent>();
            var moves = new List<EntityRegionMove>();
            bool stepCompleted;

            if (string.Equals(stepType, TravelStepType, StringComparison.Ordinal))
            {
                TravelOutcome outcome = AdvanceTravelStep(goal, payload, regionId, currentRegionKey, tick);
                if (!outcome.AnyChange)
                {
                    return null;
                }

                events.AddRange(outcome.Events);
                moves.AddRange(outcome.Moves);
                stepCompleted = outcome.StepCompleted;
            }
            else
            {
                if (!TryCompleteStep(stepType, payload, tick))
                {
                    return null;
                }

                stepCompleted = true;
            }

            if (stepCompleted)
            {
                int nextStepIndex = stepIndex + 1;
                payload.currentStepIndex = nextStepIndex;

                events.Add(CreateStepCompletedEvent(goal, regionId, stepType, stepIndex, tick));

                if (nextStepIndex >= progression.StepTypes.Count)
                {
                    events.Add(CreateGoalCompletedEvent(goal, regionId, tick));
                }
            }

            var updatedGoal = new EntityGoalState(
                goal.Id,
                goal.EntityId,
                goal.GoalKey,
                goal.Priority,
                goal.State,
                SerializePayload(payload)
            );

            return new AdvancementResult(updatedGoal, events, moves);
        }

        private TravelOutcome AdvanceTravelStep(
            EntityGoalState goal,
            GoalProgressPayload payload,
            RegionId regionId,
            string currentRegionKey,
            GameTick tick
        )
        {
            if (travelGraph == null || string.IsNullOrWhiteSpace(payload.destinationRegionKey))
            {
                return TravelOutcome.NoChange;
            }

            var events = new List<PendingWorldEvent>();
            var moves = new List<EntityRegionMove>();

            if (!payload.routeInitialized)
            {
                if (string.IsNullOrWhiteSpace(currentRegionKey))
                {
                    return TravelOutcome.NoChange;
                }

                if (string.Equals(currentRegionKey, payload.destinationRegionKey, StringComparison.Ordinal))
                {
                    payload.routeInitialized = true;
                    payload.routeRegionKeys = Array.Empty<string>();
                    payload.currentHopIndex = 0;
                    return TravelOutcome.Completed(events, moves);
                }

                IReadOnlyList<string> route = travelGraph.FindRoute(currentRegionKey, payload.destinationRegionKey);
                if (route == null || route.Count == 0)
                {
                    // Destination unreachable from the current region; stall without progress.
                    return TravelOutcome.NoChange;
                }

                payload.routeInitialized = true;
                payload.routeRegionKeys = route.ToArray();
                payload.currentHopIndex = 0;
                payload.nextHopArrivalTick = tick.Value + Math.Max(0, travelGraph.GetEdgeCost(currentRegionKey, route[0]));
                return TravelOutcome.Progressed(events, moves);
            }

            if (payload.routeRegionKeys == null || payload.currentHopIndex >= payload.routeRegionKeys.Length)
            {
                return TravelOutcome.Completed(events, moves);
            }

            if (tick.Value < payload.nextHopArrivalTick)
            {
                return TravelOutcome.NoChange;
            }

            string arrivedKey = payload.routeRegionKeys[payload.currentHopIndex];
            moves.Add(new EntityRegionMove(goal.EntityId, arrivedKey));
            events.Add(CreateRegionEnteredEvent(goal, regionId, arrivedKey, tick));
            payload.currentHopIndex += 1;

            if (payload.currentHopIndex >= payload.routeRegionKeys.Length)
            {
                return TravelOutcome.Completed(events, moves);
            }

            string nextKey = payload.routeRegionKeys[payload.currentHopIndex];
            payload.nextHopArrivalTick = tick.Value + Math.Max(0, travelGraph.GetEdgeCost(arrivedKey, nextKey));
            return TravelOutcome.Progressed(events, moves);
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

            // Remaining deferred step types (tournament registration/competition) have no handler yet,
            // so the goal stalls on them until the world-process layer exists.
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

        private static PendingWorldEvent CreateRegionEnteredEvent(
            EntityGoalState goal,
            RegionId regionId,
            string regionKey,
            GameTick tick
        )
        {
            string payloadJson = string.Format(
                "{{\"goalKey\":\"{0}\",\"regionKey\":\"{1}\"}}",
                goal.GoalKey,
                regionKey
            );
            return new PendingWorldEvent(
                regionId,
                RegionEnteredEventType,
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

            public string destinationRegionKey;
            public bool routeInitialized = false;
            public string[] routeRegionKeys;
            public int currentHopIndex = 0;
            public long nextHopArrivalTick = -1;
        }

        private sealed class TravelOutcome
        {
            private TravelOutcome(
                bool anyChange,
                bool stepCompleted,
                IReadOnlyList<PendingWorldEvent> events,
                IReadOnlyList<EntityRegionMove> moves
            )
            {
                AnyChange = anyChange;
                StepCompleted = stepCompleted;
                Events = events ?? Array.Empty<PendingWorldEvent>();
                Moves = moves ?? Array.Empty<EntityRegionMove>();
            }

            public bool AnyChange { get; }
            public bool StepCompleted { get; }
            public IReadOnlyList<PendingWorldEvent> Events { get; }
            public IReadOnlyList<EntityRegionMove> Moves { get; }

            public static TravelOutcome NoChange { get; } =
                new TravelOutcome(false, false, null, null);

            public static TravelOutcome Progressed(
                IReadOnlyList<PendingWorldEvent> events,
                IReadOnlyList<EntityRegionMove> moves
            ) => new TravelOutcome(true, false, events, moves);

            public static TravelOutcome Completed(
                IReadOnlyList<PendingWorldEvent> events,
                IReadOnlyList<EntityRegionMove> moves
            ) => new TravelOutcome(true, true, events, moves);
        }

        private sealed class AdvancementResult
        {
            public AdvancementResult(
                EntityGoalState updatedGoal,
                IReadOnlyList<PendingWorldEvent> events,
                IReadOnlyList<EntityRegionMove> entityMoves
            )
            {
                UpdatedGoal = updatedGoal;
                Events = events ?? Array.Empty<PendingWorldEvent>();
                EntityMoves = entityMoves ?? Array.Empty<EntityRegionMove>();
            }

            public EntityGoalState UpdatedGoal { get; }
            public IReadOnlyList<PendingWorldEvent> Events { get; }
            public IReadOnlyList<EntityRegionMove> EntityMoves { get; }
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
            public List<EntityRegionMove> EntityMoves { get; } = new List<EntityRegionMove>();

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
                    null,
                    EntityMoves
                );
            }
        }
    }
}
