using System.Collections.Generic;
using FantasySim.Core.Time;
using FantasySim.Persistence.SQLite;
using FantasySim.Session;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Session
{
    public sealed class CoarseRegionSchedulerTests
    {
        [Test]
        public void UpdateTicksAtMostOneDuePartitionPerCall()
        {
            var time = new FakeTimeSource { NowSeconds = 0 };
            var ticker = new RecordingTicker();
            CoarseRegionScheduler scheduler = CreateScheduler(time, ticker, Partition(1, 10), Partition(2, 10));
            scheduler.Initialize();

            PartitionTickReport first = scheduler.Update();
            PartitionTickReport second = scheduler.Update();
            PartitionTickReport third = scheduler.Update();

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(third, Is.Null);
            Assert.That(ticker.TickedPartitions, Is.EqualTo(new List<long> { 1, 2 }));
        }

        [Test]
        public void UpdateReschedulesPartitionByItsInterval()
        {
            var time = new FakeTimeSource { NowSeconds = 0 };
            var ticker = new RecordingTicker();
            CoarseRegionScheduler scheduler = CreateScheduler(time, ticker, Partition(1, 10));
            scheduler.Initialize();

            Assert.That(scheduler.Update(), Is.Not.Null);
            Assert.That(scheduler.Update(), Is.Null);

            time.NowSeconds = 9;
            Assert.That(scheduler.Update(), Is.Null);

            time.NowSeconds = 10;
            Assert.That(scheduler.Update(), Is.Not.Null);

            Assert.That(ticker.TickedPartitions, Is.EqualTo(new List<long> { 1, 1 }));
        }

        [Test]
        public void RunDuePartitionsTicksEveryDuePartitionExactlyOnce()
        {
            var time = new FakeTimeSource { NowSeconds = 0 };
            var ticker = new RecordingTicker();
            CoarseRegionScheduler scheduler = CreateScheduler(time, ticker, Partition(1, 10), Partition(2, 20), Partition(3, 30));
            scheduler.Initialize();

            IReadOnlyList<PartitionTickReport> reports = scheduler.RunDuePartitions();

            Assert.That(reports, Has.Count.EqualTo(3));
            Assert.That(ticker.TickedPartitions, Is.EqualTo(new List<long> { 1, 2, 3 }));
            Assert.That(scheduler.RunDuePartitions(), Is.Empty);
        }

        [Test]
        public void OnlyProvidedPartitionsAreScheduled()
        {
            var time = new FakeTimeSource { NowSeconds = 0 };
            var ticker = new RecordingTicker();
            CoarseRegionScheduler scheduler = CreateScheduler(time, ticker, Partition(7, 5));
            scheduler.Initialize();

            Assert.That(scheduler.ScheduledPartitionCount, Is.EqualTo(1));
            scheduler.Update();
            Assert.That(ticker.TickedPartitions, Is.EqualTo(new List<long> { 7 }));
        }

        private static CoarseRegionScheduler CreateScheduler(
            ITimeSource time,
            IPartitionTicker ticker,
            params CoarsePartitionInfo[] partitions
        )
        {
            IReadOnlyList<CoarsePartitionInfo> snapshot = partitions;
            return new CoarseRegionScheduler(time, () => snapshot, ticker);
        }

        private static CoarsePartitionInfo Partition(long id, float intervalSeconds)
        {
            return new CoarsePartitionInfo(id, $"partition_{id}", intervalSeconds, 0);
        }

        private sealed class FakeTimeSource : ITimeSource
        {
            public double NowSeconds { get; set; }
        }

        private sealed class RecordingTicker : IPartitionTicker
        {
            public List<long> TickedPartitions { get; } = new List<long>();

            public PartitionTickReport TickPartition(long partitionId)
            {
                TickedPartitions.Add(partitionId);
                return new PartitionTickReport(partitionId, new GameTick(0), new GameTick(1), 1, 1, 0);
            }
        }
    }
}
