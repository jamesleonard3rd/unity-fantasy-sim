using System.Collections.Generic;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;

namespace FantasySim.Simulation.State
{
    public sealed class RegionState
    {
        public RegionState(
            RegionId id,
            string key,
            string displayName,
            string type,
            RegionId? parentRegionId,
            float tickIntervalSeconds,
            GameTick lastTick,
            string authorityMode,
            string authorityOwner,
            GameTick? authorityUntilTick
        )
        {
            Id = id;
            Key = key;
            DisplayName = displayName;
            Type = type;
            ParentRegionId = parentRegionId;
            TickIntervalSeconds = tickIntervalSeconds;
            LastTick = lastTick;
            AuthorityMode = authorityMode;
            AuthorityOwner = authorityOwner;
            AuthorityUntilTick = authorityUntilTick;
        }

        public RegionId Id { get; }
        public string Key { get; }
        public string DisplayName { get; }
        public string Type { get; }
        public RegionId? ParentRegionId { get; }
        public float TickIntervalSeconds { get; }
        public GameTick LastTick { get; }
        public string AuthorityMode { get; }
        public string AuthorityOwner { get; }
        public GameTick? AuthorityUntilTick { get; }
    }

    public sealed class EntityNeedsState
    {
        public EntityNeedsState(EntityId entityId, float hunger, float fatigue, float mood, float stress)
        {
            EntityId = entityId;
            Hunger = hunger;
            Fatigue = fatigue;
            Mood = mood;
            Stress = stress;
        }

        public EntityId EntityId { get; }
        public float Hunger { get; }
        public float Fatigue { get; }
        public float Mood { get; }
        public float Stress { get; }
    }

    public sealed class EntityState
    {
        public EntityState(
            EntityId id,
            string templateKey,
            string displayName,
            string raceKey,
            string subraceKey,
            long ageTicks,
            bool isAlive,
            string payloadJson,
            RegionId regionId,
            float? localX,
            float? localY,
            float? localZ,
            float? headingY,
            EntityNeedsState needs
        )
        {
            Id = id;
            TemplateKey = templateKey;
            DisplayName = displayName;
            RaceKey = raceKey;
            SubraceKey = subraceKey;
            AgeTicks = ageTicks;
            IsAlive = isAlive;
            PayloadJson = payloadJson;
            RegionId = regionId;
            LocalX = localX;
            LocalY = localY;
            LocalZ = localZ;
            HeadingY = headingY;
            Needs = needs;
        }

        public EntityId Id { get; }
        public string TemplateKey { get; }
        public string DisplayName { get; }
        public string RaceKey { get; }
        public string SubraceKey { get; }
        public long AgeTicks { get; }
        public bool IsAlive { get; }
        public string PayloadJson { get; }
        public RegionId RegionId { get; }
        public float? LocalX { get; }
        public float? LocalY { get; }
        public float? LocalZ { get; }
        public float? HeadingY { get; }
        public EntityNeedsState Needs { get; }
    }

    public sealed class TickContext
    {
        public TickContext(
            GameTick currentTick,
            int ticksPerGameDay,
            long partitionId,
            string partitionKey,
            string partitionDisplayName,
            float partitionTickIntervalSeconds,
            GameTick partitionLastTick
        )
        {
            CurrentTick = currentTick;
            TicksPerGameDay = ticksPerGameDay;
            PartitionId = partitionId;
            PartitionKey = partitionKey;
            PartitionDisplayName = partitionDisplayName;
            PartitionTickIntervalSeconds = partitionTickIntervalSeconds;
            PartitionLastTick = partitionLastTick;
        }

        public GameTick CurrentTick { get; }
        public int TicksPerGameDay { get; }
        public long PartitionId { get; }
        public string PartitionKey { get; }
        public string PartitionDisplayName { get; }
        public float PartitionTickIntervalSeconds { get; }
        public GameTick PartitionLastTick { get; }
    }

    public sealed class RegionStateDelta
    {
        public RegionStateDelta(RegionId regionId, GameTick tick)
        {
            RegionId = regionId;
            Tick = tick;
        }

        public RegionId RegionId { get; }
        public GameTick Tick { get; }
    }

    public sealed class LoadedPartitionState
    {
        public LoadedPartitionState(
            TickContext tickContext,
            IReadOnlyList<RegionState> regions,
            IReadOnlyList<EntityState> entities
        )
        {
            TickContext = tickContext;
            Regions = regions;
            Entities = entities;
        }

        public TickContext TickContext { get; }
        public IReadOnlyList<RegionState> Regions { get; }
        public IReadOnlyList<EntityState> Entities { get; }
    }
}
