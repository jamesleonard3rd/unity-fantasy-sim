using System.Collections.Generic;
using FantasySim.Core.Time;
using FantasySim.Simulation.State;

namespace FantasySim.Simulation
{
    public sealed class RegionHeartbeatSystem
    {
        public const string EventType = "region.tick";
        public const int EventSignificance = 1;
        public const string EventPayloadJson = "{}";

        public RegionStateDelta Tick(RegionState region, GameTick tick)
        {
            var heartbeatEvent = new PendingWorldEvent(
                region.Id,
                EventType,
                EventSignificance,
                EventPayloadJson,
                tick
            );

            return new RegionStateDelta(
                region.Id,
                tick,
                new List<PendingWorldEvent> { heartbeatEvent }
            );
        }
    }
}
