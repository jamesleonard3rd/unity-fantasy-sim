using System;
using System.Collections.Generic;
using FantasySim.Persistence.SQLite;

namespace FantasySim.Session
{
    public sealed class CoarseRegionScheduler
    {
        private readonly ITimeSource timeSource;
        private readonly Func<IReadOnlyList<CoarsePartitionInfo>> partitionProvider;
        private readonly IPartitionTicker ticker;
        private readonly List<ScheduledPartition> scheduled = new List<ScheduledPartition>();

        public CoarseRegionScheduler(
            ITimeSource timeSource,
            Func<IReadOnlyList<CoarsePartitionInfo>> partitionProvider,
            IPartitionTicker ticker
        )
        {
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
            this.partitionProvider = partitionProvider ?? throw new ArgumentNullException(nameof(partitionProvider));
            this.ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
        }

        public int ScheduledPartitionCount => scheduled.Count;

        public void Initialize()
        {
            Refresh();
        }

        // Reloads the eligible (coarse, non-live) partitions and schedules each so it is due immediately,
        // then on its own tick interval afterward. Live partitions are excluded by the provider query.
        public void Refresh()
        {
            double now = timeSource.NowSeconds;
            var existingDueByPartition = new Dictionary<long, double>();
            foreach (ScheduledPartition entry in scheduled)
            {
                existingDueByPartition[entry.PartitionId] = entry.NextDueSeconds;
            }

            scheduled.Clear();
            foreach (CoarsePartitionInfo partition in partitionProvider())
            {
                double interval = Math.Max(0.0, partition.TickIntervalSeconds);
                double nextDue = existingDueByPartition.TryGetValue(partition.Id, out double previousDue)
                    ? previousDue
                    : now;
                scheduled.Add(new ScheduledPartition(partition.Id, interval, nextDue));
            }
        }

        // Ticks at most one due partition per call so a frame never runs several partition ticks at once.
        // Returns the report for the ticked partition, or null when nothing is due.
        public PartitionTickReport Update()
        {
            double now = timeSource.NowSeconds;

            ScheduledPartition due = null;
            foreach (ScheduledPartition entry in scheduled)
            {
                if (entry.NextDueSeconds > now)
                {
                    continue;
                }

                if (due == null
                    || entry.NextDueSeconds < due.NextDueSeconds
                    || (entry.NextDueSeconds == due.NextDueSeconds && entry.PartitionId < due.PartitionId))
                {
                    due = entry;
                }
            }

            if (due == null)
            {
                return null;
            }

            PartitionTickReport report = ticker.TickPartition(due.PartitionId);
            due.NextDueSeconds = now + due.IntervalSeconds;
            return report;
        }

        // Convenience for editor tooling: ticks every partition that is currently due, one at a time.
        public IReadOnlyList<PartitionTickReport> RunDuePartitions()
        {
            var reports = new List<PartitionTickReport>();
            PartitionTickReport report;
            while ((report = Update()) != null)
            {
                reports.Add(report);
            }

            return reports;
        }

        private sealed class ScheduledPartition
        {
            public ScheduledPartition(long partitionId, double intervalSeconds, double nextDueSeconds)
            {
                PartitionId = partitionId;
                IntervalSeconds = intervalSeconds;
                NextDueSeconds = nextDueSeconds;
            }

            public long PartitionId { get; }
            public double IntervalSeconds { get; }
            public double NextDueSeconds { get; set; }
        }
    }
}
