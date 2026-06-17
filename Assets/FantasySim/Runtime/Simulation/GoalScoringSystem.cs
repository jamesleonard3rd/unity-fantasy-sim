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
    public sealed class GoalScoringSystem
    {
        public const string EventType = "goal.primary_selected";
        public const int EventSignificance = 2;
        public const string ActiveState = "active";
        public const string InactiveState = "inactive";

        private const string TravelGoalKey = "travel_to_region";
        private const string JoinFactionGoalKey = "join_faction";
        private const string EnterTournamentGoalKey = "enter_tournament";

        private const int TravelBaseScore = 10;
        private const int JoinFactionBaseScore = 20;
        private const int EnterTournamentBaseScore = 15;
        private const int DefaultBaseScore = 5;
        private const int AmbitiousRoleBonus = 40;
        private const int RelationshipBonusPerLink = 2;
        private const int CombatTraitBonus = 15;

        private static readonly HashSet<string> AmbitiousRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "patriarch",
            "matriarch",
            "heir",
            "scion"
        };

        private static readonly HashSet<string> CombatTraitKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "strong",
            "tough",
            "quick"
        };

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
            Dictionary<long, EntityState> entityById = state.Entities.ToDictionary(entity => entity.Id.Value);
            Dictionary<long, int> outgoingPositiveByEntity = CountOutgoingPositiveRelationships(state.Relationships);
            Dictionary<long, List<EntityGoalState>> goalsByEntity = GroupGoals(state.EntityGoals);

            var deltasByRegion = new Dictionary<long, MutableGoalDelta>();
            foreach (long entityId in goalsByEntity.Keys.OrderBy(id => id))
            {
                if (!regionByEntityId.TryGetValue(entityId, out RegionId regionId))
                {
                    continue;
                }

                entityById.TryGetValue(entityId, out EntityState entity);
                GoalScoringEntityPayload payload = ParsePayload(entity?.PayloadJson);
                outgoingPositiveByEntity.TryGetValue(entityId, out int outgoingPositive);

                List<EntityGoalState> goals = goalsByEntity[entityId];
                var scoresByGoalId = new Dictionary<long, int>();
                string primaryGoalKey = null;
                int bestScore = int.MinValue;
                foreach (EntityGoalState goal in goals.OrderBy(goal => goal.GoalKey, StringComparer.Ordinal).ThenBy(goal => goal.Id))
                {
                    int score = Score(goal.GoalKey, payload, outgoingPositive);
                    scoresByGoalId[goal.Id] = score;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        primaryGoalKey = goal.GoalKey;
                    }
                }

                string previousActiveGoalKey = goals
                    .Where(goal => string.Equals(goal.State, ActiveState, StringComparison.Ordinal))
                    .OrderBy(goal => goal.Id)
                    .Select(goal => goal.GoalKey)
                    .FirstOrDefault();

                var updatedGoals = new List<EntityGoalState>();
                foreach (EntityGoalState goal in goals.OrderBy(goal => goal.Id))
                {
                    int score = scoresByGoalId[goal.Id];
                    bool isPrimary = string.Equals(goal.GoalKey, primaryGoalKey, StringComparison.Ordinal);
                    string desiredState = isPrimary ? ActiveState : InactiveState;
                    double desiredPriority = score;

                    if (!string.Equals(goal.State, desiredState, StringComparison.Ordinal) || goal.Priority != desiredPriority)
                    {
                        updatedGoals.Add(new EntityGoalState(
                            goal.Id,
                            goal.EntityId,
                            goal.GoalKey,
                            desiredPriority,
                            desiredState,
                            goal.PayloadJson
                        ));
                    }
                }

                var events = new List<PendingWorldEvent>();
                if (!string.IsNullOrEmpty(primaryGoalKey)
                    && !string.Equals(primaryGoalKey, previousActiveGoalKey, StringComparison.Ordinal))
                {
                    events.Add(CreatePrimarySelectedEvent(regionId, new EntityId(entityId), primaryGoalKey, tick));
                }

                if (updatedGoals.Count > 0 || events.Count > 0)
                {
                    MutableGoalDelta delta = GetOrCreateDelta(deltasByRegion, regionId, tick);
                    delta.UpdatedEntityGoals.AddRange(updatedGoals);
                    delta.Events.AddRange(events);
                }
            }

            return deltasByRegion.Values
                .OrderBy(delta => delta.RegionId.Value)
                .Select(delta => delta.ToRegionStateDelta())
                .ToList();
        }

        private static int Score(string goalKey, GoalScoringEntityPayload payload, int outgoingPositiveRelationships)
        {
            switch (goalKey)
            {
                case JoinFactionGoalKey:
                    int joinScore = JoinFactionBaseScore;
                    if (IsAmbitious(payload.role))
                    {
                        joinScore += AmbitiousRoleBonus;
                    }

                    joinScore += outgoingPositiveRelationships * RelationshipBonusPerLink;
                    return joinScore;
                case EnterTournamentGoalKey:
                    return EnterTournamentBaseScore + CombatTraitCount(payload.traitKeys) * CombatTraitBonus;
                case TravelGoalKey:
                    return TravelBaseScore;
                default:
                    return DefaultBaseScore;
            }
        }

        private static bool IsAmbitious(string role)
        {
            return !string.IsNullOrWhiteSpace(role) && AmbitiousRoles.Contains(role);
        }

        private static int CombatTraitCount(string[] traitKeys)
        {
            if (traitKeys == null)
            {
                return 0;
            }

            int count = 0;
            foreach (string traitKey in traitKeys)
            {
                if (!string.IsNullOrWhiteSpace(traitKey) && CombatTraitKeys.Contains(traitKey))
                {
                    count++;
                }
            }

            return count;
        }

        private static Dictionary<long, int> CountOutgoingPositiveRelationships(IReadOnlyList<RelationshipState> relationships)
        {
            var countsBySubject = new Dictionary<long, int>();
            foreach (RelationshipState relationship in relationships)
            {
                if (relationship.Opinion <= 0)
                {
                    continue;
                }

                long subjectId = relationship.Key.SubjectEntityId.Value;
                countsBySubject.TryGetValue(subjectId, out int existing);
                countsBySubject[subjectId] = existing + 1;
            }

            return countsBySubject;
        }

        private static Dictionary<long, List<EntityGoalState>> GroupGoals(IReadOnlyList<EntityGoalState> entityGoals)
        {
            var goalsByEntity = new Dictionary<long, List<EntityGoalState>>();
            foreach (EntityGoalState goal in entityGoals)
            {
                long entityId = goal.EntityId.Value;
                if (!goalsByEntity.TryGetValue(entityId, out List<EntityGoalState> goals))
                {
                    goals = new List<EntityGoalState>();
                    goalsByEntity.Add(entityId, goals);
                }

                goals.Add(goal);
            }

            return goalsByEntity;
        }

        private static MutableGoalDelta GetOrCreateDelta(
            Dictionary<long, MutableGoalDelta> deltasByRegion,
            RegionId regionId,
            GameTick tick
        )
        {
            if (!deltasByRegion.TryGetValue(regionId.Value, out MutableGoalDelta delta))
            {
                delta = new MutableGoalDelta(regionId, tick);
                deltasByRegion.Add(regionId.Value, delta);
            }

            return delta;
        }

        private static GoalScoringEntityPayload ParsePayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return new GoalScoringEntityPayload();
            }

            try
            {
                return JsonUtility.FromJson<GoalScoringEntityPayload>(payloadJson) ?? new GoalScoringEntityPayload();
            }
            catch (ArgumentException)
            {
                return new GoalScoringEntityPayload();
            }
        }

        private static PendingWorldEvent CreatePrimarySelectedEvent(
            RegionId regionId,
            EntityId entityId,
            string goalKey,
            GameTick tick
        )
        {
            string payloadJson = string.Format("{{\"goalKey\":\"{0}\"}}", goalKey);
            return new PendingWorldEvent(
                regionId,
                EventType,
                EventSignificance,
                payloadJson,
                tick,
                entityId
            );
        }

        [Serializable]
        private sealed class GoalScoringEntityPayload
        {
            public string role;
            public string[] traitKeys;
        }

        private sealed class MutableGoalDelta
        {
            public MutableGoalDelta(RegionId regionId, GameTick tick)
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
