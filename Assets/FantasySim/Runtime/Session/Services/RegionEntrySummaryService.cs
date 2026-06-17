using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Persistence.SQLite;

namespace FantasySim.Session
{
    // Pure builder that turns coarse-tick world events into a concise "While you were away..." summary.
    // It has no persistence or engine dependencies so it can be unit tested directly.
    public sealed class RegionEntrySummaryService
    {
        public const int DefaultMinSignificance = 2;
        public const string DefaultHeader = "While you were away...";

        private static readonly IReadOnlyDictionary<string, EventLabel> KnownLabels =
            new Dictionary<string, EventLabel>
            {
                ["relationship.opinion_changed"] = new EventLabel("relationship shifted", "relationships shifted"),
                ["goal.primary_selected"] = new EventLabel("NPC set a new goal", "NPCs set new goals"),
                ["goal.step_completed"] = new EventLabel("goal step completed", "goal steps completed"),
                ["goal.completed"] = new EventLabel("goal completed", "goals completed"),
                ["interaction.occurred"] = new EventLabel("interaction happened", "interactions happened"),
                ["region.tick"] = new EventLabel("region heartbeat", "region heartbeats")
            };

        public RegionEntrySummary Build(
            IReadOnlyList<WorldEventRecord> events,
            int minSignificance = DefaultMinSignificance,
            string header = DefaultHeader
        )
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            List<WorldEventRecord> relevant = events
                .Where(worldEvent => worldEvent.Significance >= minSignificance)
                .ToList();

            // Group by type preserving the most-significant-first, then most-frequent ordering so the
            // headline lines describe the events the player is most likely to care about.
            List<SummaryLine> lines = relevant
                .GroupBy(worldEvent => worldEvent.Type)
                .Select(group => new SummaryLine(
                    group.Key,
                    group.Count(),
                    group.Max(worldEvent => worldEvent.Significance),
                    DescribeGroup(group.Key, group.Count())
                ))
                .OrderByDescending(line => line.MaxSignificance)
                .ThenByDescending(line => line.Count)
                .ThenBy(line => line.EventType, StringComparer.Ordinal)
                .ToList();

            return new RegionEntrySummary(header, relevant.Count, lines);
        }

        private static string DescribeGroup(string eventType, int count)
        {
            if (KnownLabels.TryGetValue(eventType, out EventLabel label))
            {
                return $"{count} {(count == 1 ? label.Singular : label.Plural)}.";
            }

            return $"{count} {eventType} event{(count == 1 ? string.Empty : "s")}.";
        }

        private readonly struct EventLabel
        {
            public EventLabel(string singular, string plural)
            {
                Singular = singular;
                Plural = plural;
            }

            public string Singular { get; }
            public string Plural { get; }
        }
    }

    public sealed class RegionEntrySummary
    {
        public RegionEntrySummary(string header, int totalEvents, IReadOnlyList<SummaryLine> lines)
        {
            Header = header;
            TotalEvents = totalEvents;
            Lines = lines ?? new List<SummaryLine>();
        }

        public string Header { get; }
        public int TotalEvents { get; }
        public IReadOnlyList<SummaryLine> Lines { get; }

        public bool IsEmpty => Lines.Count == 0;

        public string ToDisplayText()
        {
            if (IsEmpty)
            {
                return $"{Header}\nNothing notable happened.";
            }

            return Header + "\n" + string.Join("\n", Lines.Select(line => "- " + line.Text));
        }
    }

    public sealed class SummaryLine
    {
        public SummaryLine(string eventType, int count, int maxSignificance, string text)
        {
            EventType = eventType;
            Count = count;
            MaxSignificance = maxSignificance;
            Text = text;
        }

        public string EventType { get; }
        public int Count { get; }
        public int MaxSignificance { get; }
        public string Text { get; }
    }
}
