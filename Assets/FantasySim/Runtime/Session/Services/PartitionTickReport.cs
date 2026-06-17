using FantasySim.Core.Time;

namespace FantasySim.Session
{
    public sealed class PartitionTickReport
    {
        public PartitionTickReport(
            long partitionId,
            GameTick previousTick,
            GameTick currentTick,
            int eventsInserted,
            int regionsProcessed,
            double elapsedMilliseconds
        )
        {
            PartitionId = partitionId;
            PreviousTick = previousTick;
            CurrentTick = currentTick;
            EventsInserted = eventsInserted;
            RegionsProcessed = regionsProcessed;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public long PartitionId { get; }
        public GameTick PreviousTick { get; }
        public GameTick CurrentTick { get; }
        public int EventsInserted { get; }
        public int RegionsProcessed { get; }
        public double ElapsedMilliseconds { get; }
    }
}
