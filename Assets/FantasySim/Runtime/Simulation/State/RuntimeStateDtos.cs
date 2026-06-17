using System;
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
            float? headingY
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
        public RegionStateDelta(
            RegionId regionId,
            GameTick tick,
            IReadOnlyList<PendingWorldEvent> events,
            IReadOnlyList<RelationshipState> updatedRelationships = null,
            IReadOnlyList<RelationshipTermState> updatedRelationshipTerms = null,
            IReadOnlyList<long> deletedRelationshipTermIds = null,
            IReadOnlyList<EntityGoalState> updatedEntityGoals = null,
            IReadOnlyList<long> deletedEntityGoalIds = null
        )
        {
            RegionId = regionId;
            Tick = tick;
            Events = events ?? new List<PendingWorldEvent>();
            UpdatedRelationships = updatedRelationships ?? new List<RelationshipState>();
            UpdatedRelationshipTerms = updatedRelationshipTerms ?? new List<RelationshipTermState>();
            DeletedRelationshipTermIds = deletedRelationshipTermIds ?? new List<long>();
            UpdatedEntityGoals = updatedEntityGoals ?? new List<EntityGoalState>();
            DeletedEntityGoalIds = deletedEntityGoalIds ?? new List<long>();
        }

        public RegionId RegionId { get; }
        public GameTick Tick { get; }
        public IReadOnlyList<PendingWorldEvent> Events { get; }
        public IReadOnlyList<RelationshipState> UpdatedRelationships { get; }
        public IReadOnlyList<RelationshipTermState> UpdatedRelationshipTerms { get; }
        public IReadOnlyList<long> DeletedRelationshipTermIds { get; }
        public IReadOnlyList<EntityGoalState> UpdatedEntityGoals { get; }
        public IReadOnlyList<long> DeletedEntityGoalIds { get; }
    }

    public sealed class PendingWorldEvent
    {
        public PendingWorldEvent(
            RegionId? regionId,
            string type,
            int significance,
            string payloadJson,
            GameTick occurredTick,
            EntityId? subjectEntityId = null,
            EntityId? targetEntityId = null
        )
        {
            RegionId = regionId;
            Type = type;
            Significance = significance;
            PayloadJson = payloadJson;
            OccurredTick = occurredTick;
            SubjectEntityId = subjectEntityId;
            TargetEntityId = targetEntityId;
        }

        public RegionId? RegionId { get; }
        public string Type { get; }
        public int Significance { get; }
        public string PayloadJson { get; }
        public GameTick OccurredTick { get; }
        public EntityId? SubjectEntityId { get; }
        public EntityId? TargetEntityId { get; }
    }

    public sealed class LoadedPartitionState
    {
        public LoadedPartitionState(
            TickContext tickContext,
            IReadOnlyList<RegionState> regions,
            IReadOnlyList<EntityState> entities,
            IReadOnlyList<RelationshipState> relationships = null,
            IReadOnlyList<RelationshipTermState> relationshipTerms = null,
            IReadOnlyList<EntityGoalState> entityGoals = null
        )
        {
            TickContext = tickContext;
            Regions = regions;
            Entities = entities;
            Relationships = relationships ?? new List<RelationshipState>();
            RelationshipTerms = relationshipTerms ?? new List<RelationshipTermState>();
            EntityGoals = entityGoals ?? new List<EntityGoalState>();
        }

        public TickContext TickContext { get; }
        public IReadOnlyList<RegionState> Regions { get; }
        public IReadOnlyList<EntityState> Entities { get; }
        public IReadOnlyList<RelationshipState> Relationships { get; }
        public IReadOnlyList<RelationshipTermState> RelationshipTerms { get; }
        public IReadOnlyList<EntityGoalState> EntityGoals { get; }
    }

    public readonly struct RelationshipKey : IEquatable<RelationshipKey>
    {
        public RelationshipKey(EntityId subjectEntityId, EntityId targetEntityId)
        {
            SubjectEntityId = subjectEntityId;
            TargetEntityId = targetEntityId;
        }

        public EntityId SubjectEntityId { get; }
        public EntityId TargetEntityId { get; }

        public bool Equals(RelationshipKey other)
        {
            return SubjectEntityId.Value == other.SubjectEntityId.Value
                && TargetEntityId.Value == other.TargetEntityId.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is RelationshipKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SubjectEntityId.Value.GetHashCode() * 397) ^ TargetEntityId.Value.GetHashCode();
            }
        }
    }

    public sealed class RelationshipState
    {
        public RelationshipState(
            RelationshipKey key,
            int opinion,
            int familiarity,
            GameTick updatedTick
        )
        {
            Key = key;
            Opinion = opinion;
            Familiarity = familiarity;
            UpdatedTick = updatedTick;
        }

        public RelationshipKey Key { get; }
        public int Opinion { get; }
        public int Familiarity { get; }
        public GameTick UpdatedTick { get; }
    }

    public sealed class RelationshipTermState
    {
        public RelationshipTermState(
            long id,
            RelationshipKey key,
            string type,
            int value,
            double decayPerTick,
            GameTick? expiresTick,
            string payloadJson
        )
        {
            Id = id;
            Key = key;
            Type = type;
            Value = value;
            DecayPerTick = decayPerTick;
            ExpiresTick = expiresTick;
            PayloadJson = payloadJson;
        }

        public long Id { get; }
        public RelationshipKey Key { get; }
        public string Type { get; }
        public int Value { get; }
        public double DecayPerTick { get; }
        public GameTick? ExpiresTick { get; }
        public string PayloadJson { get; }
    }

    public sealed class EntityGoalState
    {
        public EntityGoalState(
            long id,
            EntityId entityId,
            string goalKey,
            double priority,
            string state,
            string payloadJson
        )
        {
            Id = id;
            EntityId = entityId;
            GoalKey = goalKey;
            Priority = priority;
            State = state;
            PayloadJson = payloadJson;
        }

        public long Id { get; }
        public EntityId EntityId { get; }
        public string GoalKey { get; }
        public double Priority { get; }
        public string State { get; }
        public string PayloadJson { get; }
    }
}
