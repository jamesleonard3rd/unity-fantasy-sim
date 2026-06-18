using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Time;
using FantasySim.Simulation.State;

namespace FantasySim.Simulation
{
    public sealed class StarterWorldSimulationPipeline
    {
        private readonly RelationshipDynamicsSystem relationshipDynamicsSystem;
        private readonly GoalScoringSystem goalScoringSystem;
        private readonly GoalAdvancementSystem goalAdvancementSystem;
        private readonly PairwiseInteractionSystem pairwiseInteractionSystem;
        private readonly RegionHeartbeatSystem heartbeatSystem;

        public StarterWorldSimulationPipeline(
            RelationshipDynamicsSystem relationshipDynamicsSystem = null,
            GoalScoringSystem goalScoringSystem = null,
            GoalAdvancementSystem goalAdvancementSystem = null,
            PairwiseInteractionSystem pairwiseInteractionSystem = null,
            RegionHeartbeatSystem heartbeatSystem = null
        )
        {
            this.relationshipDynamicsSystem = relationshipDynamicsSystem ?? new RelationshipDynamicsSystem();
            this.goalScoringSystem = goalScoringSystem ?? new GoalScoringSystem();
            this.goalAdvancementSystem = goalAdvancementSystem ?? new GoalAdvancementSystem();
            this.pairwiseInteractionSystem = pairwiseInteractionSystem ?? new PairwiseInteractionSystem();
            this.heartbeatSystem = heartbeatSystem ?? new RegionHeartbeatSystem();
        }

        public PartitionTickDelta Tick(LoadedPartitionState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var targetTick = new GameTick(state.TickContext.CurrentTick.Value + 1);
            var regionDeltas = new List<RegionStateDelta>(state.Regions.Count);

            Dictionary<long, RegionStateDelta> relationshipDeltasByRegion = IndexByRegion(
                relationshipDynamicsSystem.Tick(state, targetTick)
            );
            Dictionary<long, RegionStateDelta> goalScoringDeltasByRegion = IndexByRegion(
                goalScoringSystem.Tick(state, targetTick)
            );
            Dictionary<long, RegionStateDelta> goalAdvancementDeltasByRegion = IndexByRegion(
                goalAdvancementSystem.Tick(state, targetTick)
            );
            Dictionary<long, RegionStateDelta> pairwiseDeltasByRegion = IndexByRegion(
                pairwiseInteractionSystem.Tick(state, targetTick)
            );

            foreach (RegionState region in state.Regions)
            {
                RegionStateDelta heartbeatDelta = heartbeatSystem.Tick(region, targetTick);
                relationshipDeltasByRegion.TryGetValue(region.Id.Value, out RegionStateDelta relationshipDelta);
                goalScoringDeltasByRegion.TryGetValue(region.Id.Value, out RegionStateDelta goalScoringDelta);
                goalAdvancementDeltasByRegion.TryGetValue(region.Id.Value, out RegionStateDelta goalAdvancementDelta);
                pairwiseDeltasByRegion.TryGetValue(region.Id.Value, out RegionStateDelta pairwiseDelta);

                regionDeltas.Add(MergeRegionDeltas(
                    region.Id.Value,
                    targetTick,
                    relationshipDelta,
                    goalScoringDelta,
                    goalAdvancementDelta,
                    pairwiseDelta,
                    heartbeatDelta
                ));
            }

            return new PartitionTickDelta(
                state.TickContext.CurrentTick,
                targetTick,
                regionDeltas
            );
        }

        private static Dictionary<long, RegionStateDelta> IndexByRegion(IReadOnlyList<RegionStateDelta> deltas)
        {
            var indexed = new Dictionary<long, RegionStateDelta>();
            if (deltas == null)
            {
                return indexed;
            }

            foreach (RegionStateDelta delta in deltas)
            {
                indexed[delta.RegionId.Value] = delta;
            }

            return indexed;
        }

        private static RegionStateDelta MergeRegionDeltas(
            long regionIdValue,
            GameTick tick,
            RegionStateDelta relationshipDelta,
            RegionStateDelta goalScoringDelta,
            RegionStateDelta goalAdvancementDelta,
            RegionStateDelta pairwiseDelta,
            RegionStateDelta heartbeatDelta
        )
        {
            var events = new List<PendingWorldEvent>();
            var updatedRelationships = new List<RelationshipState>();
            var updatedRelationshipTerms = new List<RelationshipTermState>();
            var deletedRelationshipTermIds = new List<long>();
            var deletedEntityGoalIds = new List<long>();
            var updatedEntityPositions = new List<EntityRegionMove>();

            var orderedSources = new[]
            {
                relationshipDelta,
                goalScoringDelta,
                goalAdvancementDelta,
                pairwiseDelta,
                heartbeatDelta
            };

            foreach (RegionStateDelta source in orderedSources)
            {
                if (source == null)
                {
                    continue;
                }

                events.AddRange(source.Events);
                updatedRelationships.AddRange(source.UpdatedRelationships);
                updatedRelationshipTerms.AddRange(source.UpdatedRelationshipTerms);
                deletedRelationshipTermIds.AddRange(source.DeletedRelationshipTermIds);
                deletedEntityGoalIds.AddRange(source.DeletedEntityGoalIds);
                updatedEntityPositions.AddRange(source.UpdatedEntityPositions);
            }

            IReadOnlyList<EntityGoalState> updatedEntityGoals = MergeEntityGoals(
                goalScoringDelta,
                goalAdvancementDelta
            );

            return new RegionStateDelta(
                new FantasySim.Core.Ids.RegionId(regionIdValue),
                tick,
                events,
                updatedRelationships,
                updatedRelationshipTerms,
                deletedRelationshipTermIds,
                updatedEntityGoals,
                deletedEntityGoalIds,
                updatedEntityPositions
            );
        }

        private static IReadOnlyList<EntityGoalState> MergeEntityGoals(
            RegionStateDelta goalScoringDelta,
            RegionStateDelta goalAdvancementDelta
        )
        {
            var goalsById = new Dictionary<long, EntityGoalState>();

            if (goalScoringDelta != null)
            {
                foreach (EntityGoalState goal in goalScoringDelta.UpdatedEntityGoals)
                {
                    goalsById[goal.Id] = goal;
                }
            }

            if (goalAdvancementDelta != null)
            {
                foreach (EntityGoalState advancement in goalAdvancementDelta.UpdatedEntityGoals)
                {
                    if (goalsById.TryGetValue(advancement.Id, out EntityGoalState scoring))
                    {
                        // Scoring owns state/priority; advancement owns payload (step progress).
                        goalsById[advancement.Id] = new EntityGoalState(
                            scoring.Id,
                            scoring.EntityId,
                            scoring.GoalKey,
                            scoring.Priority,
                            scoring.State,
                            advancement.PayloadJson
                        );
                    }
                    else
                    {
                        goalsById[advancement.Id] = advancement;
                    }
                }
            }

            return goalsById.Values.OrderBy(goal => goal.Id).ToList();
        }
    }

    public sealed class PartitionTickDelta
    {
        public PartitionTickDelta(
            GameTick previousTick,
            GameTick targetTick,
            IReadOnlyList<RegionStateDelta> regionDeltas
        )
        {
            PreviousTick = previousTick;
            TargetTick = targetTick;
            RegionDeltas = regionDeltas ?? new List<RegionStateDelta>();
        }

        public GameTick PreviousTick { get; }
        public GameTick TargetTick { get; }
        public IReadOnlyList<RegionStateDelta> RegionDeltas { get; }

        public int EventCount
        {
            get
            {
                int count = 0;
                foreach (RegionStateDelta delta in RegionDeltas)
                {
                    count += delta.Events.Count;
                }

                return count;
            }
        }
    }
}
