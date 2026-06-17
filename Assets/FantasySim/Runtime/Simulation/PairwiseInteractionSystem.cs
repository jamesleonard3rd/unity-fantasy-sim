using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation.State;
using EntityId = FantasySim.Core.Ids.EntityId;

namespace FantasySim.Simulation
{
    public sealed class PairwiseInteractionSystem
    {
        public const string EventType = "interaction.occurred";
        public const int EventSignificance = 1;
        public const string InteractionTermType = "interaction";

        public const int DefaultCandidateCap = 30;
        public const int DefaultMaxInteractionsPerEntity = 1;
        public const int DefaultInteractionTermValue = 2;
        public const double DefaultInteractionTermDecayPerTick = 1;
        public const long DefaultInteractionTermLifetimeTicks = 3;

        private readonly int candidateCap;
        private readonly int maxInteractionsPerEntity;
        private readonly int interactionTermValue;
        private readonly double interactionTermDecayPerTick;
        private readonly long interactionTermLifetimeTicks;

        public PairwiseInteractionSystem(
            int candidateCap = DefaultCandidateCap,
            int maxInteractionsPerEntity = DefaultMaxInteractionsPerEntity,
            int interactionTermValue = DefaultInteractionTermValue,
            double interactionTermDecayPerTick = DefaultInteractionTermDecayPerTick,
            long interactionTermLifetimeTicks = DefaultInteractionTermLifetimeTicks
        )
        {
            this.candidateCap = Math.Max(1, candidateCap);
            this.maxInteractionsPerEntity = Math.Max(0, maxInteractionsPerEntity);
            this.interactionTermValue = interactionTermValue;
            this.interactionTermDecayPerTick = interactionTermDecayPerTick;
            this.interactionTermLifetimeTicks = Math.Max(1, interactionTermLifetimeTicks);
        }

        public IReadOnlyList<RegionStateDelta> Tick(LoadedPartitionState state, GameTick tick)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (maxInteractionsPerEntity == 0)
            {
                return new List<RegionStateDelta>();
            }

            Dictionary<long, List<EntityState>> entitiesByRegion = GroupEntitiesByRegion(state.Entities);
            Dictionary<RelationshipKey, int> opinionByPair = state.Relationships.ToDictionary(
                relationship => relationship.Key,
                relationship => relationship.Opinion
            );

            var deltasByRegion = new Dictionary<long, MutableInteractionDelta>();
            foreach (long regionId in entitiesByRegion.Keys.OrderBy(id => id))
            {
                List<EntityState> regionEntities = entitiesByRegion[regionId];
                if (regionEntities.Count < 2)
                {
                    continue;
                }

                foreach (EntityState subject in regionEntities.OrderBy(entity => entity.Id.Value))
                {
                    IReadOnlyList<EntityState> targets = SelectInteractionTargets(subject, regionEntities, opinionByPair);
                    if (targets.Count == 0)
                    {
                        continue;
                    }

                    MutableInteractionDelta delta = GetOrCreateDelta(deltasByRegion, new RegionId(regionId), tick);
                    foreach (EntityState target in targets)
                    {
                        delta.UpdatedRelationshipTerms.Add(CreateInteractionTerm(subject.Id, target.Id, tick));
                        delta.Events.Add(CreateInteractionEvent(new RegionId(regionId), subject.Id, target.Id, tick));
                    }
                }
            }

            return deltasByRegion.Values
                .OrderBy(delta => delta.RegionId.Value)
                .Select(delta => delta.ToRegionStateDelta())
                .ToList();
        }

        private IReadOnlyList<EntityState> SelectInteractionTargets(
            EntityState subject,
            IReadOnlyList<EntityState> regionEntities,
            IReadOnlyDictionary<RelationshipKey, int> opinionByPair
        )
        {
            var candidates = new List<CandidateScore>();
            foreach (EntityState candidate in regionEntities)
            {
                if (candidate.Id.Value == subject.Id.Value)
                {
                    continue;
                }

                bool hasRelationship = opinionByPair.TryGetValue(
                    new RelationshipKey(subject.Id, candidate.Id),
                    out int opinion
                );
                candidates.Add(new CandidateScore(candidate, hasRelationship, hasRelationship ? opinion : 0));
            }

            // Prefer existing relationships and higher opinion, then fall back to a stable id ordering so the
            // capped candidate list and selected interactions are deterministic for tests.
            IEnumerable<CandidateScore> ranked = candidates
                .OrderByDescending(candidate => candidate.HasRelationship)
                .ThenByDescending(candidate => candidate.Opinion)
                .ThenBy(candidate => candidate.Entity.Id.Value)
                .Take(candidateCap);

            return ranked
                .Take(maxInteractionsPerEntity)
                .Select(candidate => candidate.Entity)
                .ToList();
        }

        private RelationshipTermState CreateInteractionTerm(EntityId subjectId, EntityId targetId, GameTick tick)
        {
            return new RelationshipTermState(
                0,
                new RelationshipKey(subjectId, targetId),
                InteractionTermType,
                interactionTermValue,
                interactionTermDecayPerTick,
                new GameTick(tick.Value + interactionTermLifetimeTicks),
                "{}"
            );
        }

        private static PendingWorldEvent CreateInteractionEvent(
            RegionId regionId,
            EntityId subjectId,
            EntityId targetId,
            GameTick tick
        )
        {
            string payloadJson = string.Format(
                "{{\"subjectEntityId\":{0},\"targetEntityId\":{1}}}",
                subjectId.Value,
                targetId.Value
            );
            return new PendingWorldEvent(
                regionId,
                EventType,
                EventSignificance,
                payloadJson,
                tick,
                subjectId,
                targetId
            );
        }

        private static Dictionary<long, List<EntityState>> GroupEntitiesByRegion(IReadOnlyList<EntityState> entities)
        {
            var entitiesByRegion = new Dictionary<long, List<EntityState>>();
            foreach (EntityState entity in entities)
            {
                if (!entity.IsAlive)
                {
                    continue;
                }

                long regionId = entity.RegionId.Value;
                if (!entitiesByRegion.TryGetValue(regionId, out List<EntityState> regionEntities))
                {
                    regionEntities = new List<EntityState>();
                    entitiesByRegion.Add(regionId, regionEntities);
                }

                regionEntities.Add(entity);
            }

            return entitiesByRegion;
        }

        private static MutableInteractionDelta GetOrCreateDelta(
            Dictionary<long, MutableInteractionDelta> deltasByRegion,
            RegionId regionId,
            GameTick tick
        )
        {
            if (!deltasByRegion.TryGetValue(regionId.Value, out MutableInteractionDelta delta))
            {
                delta = new MutableInteractionDelta(regionId, tick);
                deltasByRegion.Add(regionId.Value, delta);
            }

            return delta;
        }

        private readonly struct CandidateScore
        {
            public CandidateScore(EntityState entity, bool hasRelationship, int opinion)
            {
                Entity = entity;
                HasRelationship = hasRelationship;
                Opinion = opinion;
            }

            public EntityState Entity { get; }
            public bool HasRelationship { get; }
            public int Opinion { get; }
        }

        private sealed class MutableInteractionDelta
        {
            public MutableInteractionDelta(RegionId regionId, GameTick tick)
            {
                RegionId = regionId;
                Tick = tick;
            }

            public RegionId RegionId { get; }
            public GameTick Tick { get; }
            public List<PendingWorldEvent> Events { get; } = new List<PendingWorldEvent>();
            public List<RelationshipTermState> UpdatedRelationshipTerms { get; } = new List<RelationshipTermState>();

            public RegionStateDelta ToRegionStateDelta()
            {
                return new RegionStateDelta(
                    RegionId,
                    Tick,
                    Events,
                    null,
                    UpdatedRelationshipTerms,
                    null,
                    null,
                    null
                );
            }
        }
    }
}
