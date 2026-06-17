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
        private readonly RegionHeartbeatSystem heartbeatSystem;

        public StarterWorldSimulationPipeline(
            RelationshipDynamicsSystem relationshipDynamicsSystem = null,
            RegionHeartbeatSystem heartbeatSystem = null
        )
        {
            this.relationshipDynamicsSystem = relationshipDynamicsSystem ?? new RelationshipDynamicsSystem();
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
            Dictionary<long, RegionStateDelta> relationshipDeltasByRegion = relationshipDynamicsSystem
                .Tick(state, targetTick)
                .ToDictionary(delta => delta.RegionId.Value);

            foreach (RegionState region in state.Regions)
            {
                RegionStateDelta heartbeatDelta = heartbeatSystem.Tick(region, targetTick);
                if (relationshipDeltasByRegion.TryGetValue(region.Id.Value, out RegionStateDelta relationshipDelta))
                {
                    regionDeltas.Add(MergeRegionDeltas(relationshipDelta, heartbeatDelta));
                    continue;
                }

                regionDeltas.Add(heartbeatDelta);
            }

            return new PartitionTickDelta(
                state.TickContext.CurrentTick,
                targetTick,
                regionDeltas
            );
        }

        private static RegionStateDelta MergeRegionDeltas(RegionStateDelta relationshipDelta, RegionStateDelta heartbeatDelta)
        {
            var events = new List<PendingWorldEvent>(
                relationshipDelta.Events.Count + heartbeatDelta.Events.Count
            );
            events.AddRange(relationshipDelta.Events);
            events.AddRange(heartbeatDelta.Events);

            return new RegionStateDelta(
                heartbeatDelta.RegionId,
                heartbeatDelta.Tick,
                events,
                relationshipDelta.UpdatedRelationships,
                relationshipDelta.UpdatedRelationshipTerms,
                relationshipDelta.DeletedRelationshipTermIds
            );
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
