using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Persistence.SQLite;
using FantasySim.Session;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Session
{
    public sealed class RegionEntrySummaryServiceTests
    {
        [Test]
        public void BuildFiltersBelowMinimumSignificance()
        {
            var service = new RegionEntrySummaryService();
            List<WorldEventRecord> events = new List<WorldEventRecord>
            {
                CreateEvent("region.tick", 1, 1),
                CreateEvent("interaction.occurred", 1, 2),
                CreateEvent("goal.primary_selected", 2, 3)
            };

            RegionEntrySummary summary = service.Build(events, minSignificance: 2);

            Assert.That(summary.TotalEvents, Is.EqualTo(1));
            Assert.That(summary.Lines.Count, Is.EqualTo(1));
            Assert.That(summary.Lines[0].EventType, Is.EqualTo("goal.primary_selected"));
        }

        [Test]
        public void BuildGroupsByTypeAndCounts()
        {
            var service = new RegionEntrySummaryService();
            List<WorldEventRecord> events = new List<WorldEventRecord>
            {
                CreateEvent("goal.primary_selected", 2, 1),
                CreateEvent("goal.primary_selected", 2, 2),
                CreateEvent("relationship.opinion_changed", 4, 3)
            };

            RegionEntrySummary summary = service.Build(events);

            SummaryLine goalLine = summary.Lines.Single(line => line.EventType == "goal.primary_selected");
            Assert.That(goalLine.Count, Is.EqualTo(2));

            // Most significant group should sort first.
            Assert.That(summary.Lines[0].EventType, Is.EqualTo("relationship.opinion_changed"));
        }

        [Test]
        public void BuildReturnsEmptySummaryWhenNothingNotable()
        {
            var service = new RegionEntrySummaryService();
            List<WorldEventRecord> events = new List<WorldEventRecord>
            {
                CreateEvent("region.tick", 1, 1)
            };

            RegionEntrySummary summary = service.Build(events);

            Assert.That(summary.IsEmpty, Is.True);
            Assert.That(summary.ToDisplayText(), Does.Contain("Nothing notable"));
        }

        private static WorldEventRecord CreateEvent(string type, int significance, long tick)
        {
            return new WorldEventRecord(
                tick,
                new RegionId(1),
                type,
                new EntityId(1),
                null,
                significance,
                "{}",
                new GameTick(tick)
            );
        }
    }
}
