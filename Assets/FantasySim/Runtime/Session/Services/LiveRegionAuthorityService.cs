using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Persistence.SQLite;
using FantasySim.Simulation.State;

namespace FantasySim.Session
{
    // Coordinates the handoff between coarse simulation and a live region:
    // marks a region live so the coarse scheduler skips its partition, exposes the
    // region's entities for spawning, and writes positions back while restoring coarse
    // authority on exit. The scheduler skip itself is handled by SimulationPartitionRepository.
    public sealed class LiveRegionAuthorityService
    {
        public const string DefaultPartitionKey = StarterWorldReadRepository.StarterPartitionKey;
        public const string DefaultOwner = "player_session";

        private readonly StarterWorldReadRepository readRepository;
        private readonly LiveRegionRepository liveRegionRepository;
        private readonly WorldEventReadRepository eventReadRepository;
        private readonly RegionEntrySummaryService summaryService;

        public LiveRegionAuthorityService(DatabasePathProvider pathProvider = null)
            : this(
                new StarterWorldReadRepository(pathProvider),
                new LiveRegionRepository(pathProvider),
                new WorldEventReadRepository(pathProvider),
                new RegionEntrySummaryService()
            )
        {
        }

        public LiveRegionAuthorityService(
            StarterWorldReadRepository readRepository,
            LiveRegionRepository liveRegionRepository,
            WorldEventReadRepository eventReadRepository,
            RegionEntrySummaryService summaryService
        )
        {
            this.readRepository = readRepository ?? throw new ArgumentNullException(nameof(readRepository));
            this.liveRegionRepository = liveRegionRepository ?? throw new ArgumentNullException(nameof(liveRegionRepository));
            this.eventReadRepository = eventReadRepository ?? throw new ArgumentNullException(nameof(eventReadRepository));
            this.summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
        }

        // Marks the resolved region live and returns the entities to spawn plus a
        // "while you were away" summary of events since the region was last seen.
        public LiveRegionEntry EnterLiveRegion(
            string partitionKey = DefaultPartitionKey,
            string regionKey = null,
            string owner = DefaultOwner
        )
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new ArgumentException("Partition key is required.", nameof(partitionKey));
            }

            LoadedPartitionState state = readRepository.LoadPartition(partitionKey);
            RegionState region = ResolveRegion(state, regionKey);

            List<EntityState> entities = state.Entities
                .Where(entity => entity.RegionId.Value == region.Id.Value)
                .ToList();

            GameTick? lastSeen = liveRegionRepository.GetRegionLastSeenTick(region.Id);
            var eventQuery = new WorldEventQuery
            {
                RegionId = region.Id,
                MinTick = lastSeen.HasValue ? new GameTick(lastSeen.Value.Value + 1) : (GameTick?)null
            };
            IReadOnlyList<WorldEventRecord> events = eventReadRepository.Query(eventQuery);
            RegionEntrySummary summary = summaryService.Build(events);

            liveRegionRepository.SetRegionAuthority(region.Id, LiveRegionRepository.LiveAuthorityMode, owner);

            return new LiveRegionEntry(region, entities, summary, state.TickContext.CurrentTick);
        }

        // Writes the supplied positions, restores coarse authority, and records the tick the
        // region's events were seen through so the next entry summary starts fresh.
        public int ExitLiveRegion(
            RegionId regionId,
            IReadOnlyList<EntityPositionUpdate> positions,
            GameTick seenThroughTick
        )
        {
            int positionsWritten = liveRegionRepository.WriteEntityPositions(
                positions ?? new List<EntityPositionUpdate>()
            );
            liveRegionRepository.SetRegionAuthority(regionId, LiveRegionRepository.CoarseAuthorityMode);
            liveRegionRepository.UpsertRegionLastSeenTick(regionId, seenThroughTick);
            return positionsWritten;
        }

        private static RegionState ResolveRegion(LoadedPartitionState state, string regionKey)
        {
            if (state.Regions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Partition '{state.TickContext.PartitionKey}' has no regions to enter."
                );
            }

            if (string.IsNullOrWhiteSpace(regionKey))
            {
                return state.Regions[0];
            }

            RegionState match = state.Regions.FirstOrDefault(region => region.Key == regionKey);
            if (match == null)
            {
                throw new InvalidOperationException(
                    $"Region '{regionKey}' is not part of partition '{state.TickContext.PartitionKey}'."
                );
            }

            return match;
        }
    }

    public sealed class LiveRegionEntry
    {
        public LiveRegionEntry(
            RegionState region,
            IReadOnlyList<EntityState> entities,
            RegionEntrySummary summary,
            GameTick currentTick
        )
        {
            Region = region;
            Entities = entities ?? new List<EntityState>();
            Summary = summary;
            CurrentTick = currentTick;
        }

        public RegionState Region { get; }
        public IReadOnlyList<EntityState> Entities { get; }
        public RegionEntrySummary Summary { get; }
        public GameTick CurrentTick { get; }
    }
}
