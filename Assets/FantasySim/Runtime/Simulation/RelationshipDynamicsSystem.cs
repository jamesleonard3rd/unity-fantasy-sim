using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation.State;

namespace FantasySim.Simulation
{
    public sealed class RelationshipDynamicsSystem
    {
        public const string EventType = "relationship.opinion_changed";
        public const int EventSignificance = 4;
        public const int SignificantOpinionChangeThreshold = 10;

        public IReadOnlyList<RegionStateDelta> Tick(LoadedPartitionState state, GameTick tick)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Dictionary<long, RegionId> regionByEntityId = state.Entities.ToDictionary(
                entity => entity.Id.Value,
                entity => entity.RegionId
            );
            Dictionary<RelationshipKey, RelationshipState> relationshipsByKey = state.Relationships.ToDictionary(
                relationship => relationship.Key
            );
            Dictionary<RelationshipKey, List<RelationshipTermState>> termsByRelationship = GroupTerms(state.RelationshipTerms);
            var keys = new HashSet<RelationshipKey>(relationshipsByKey.Keys);
            foreach (RelationshipKey key in termsByRelationship.Keys)
            {
                keys.Add(key);
            }

            var deltasByRegion = new Dictionary<long, MutableRelationshipDelta>();
            foreach (RelationshipKey key in keys.OrderBy(key => key.SubjectEntityId.Value).ThenBy(key => key.TargetEntityId.Value))
            {
                if (!regionByEntityId.TryGetValue(key.SubjectEntityId.Value, out RegionId subjectRegionId))
                {
                    continue;
                }

                RelationshipState currentRelationship = relationshipsByKey.TryGetValue(key, out RelationshipState relationship)
                    ? relationship
                    : new RelationshipState(key, 0, 0, state.TickContext.CurrentTick);
                List<RelationshipTermState> terms = termsByRelationship.TryGetValue(key, out List<RelationshipTermState> relationshipTerms)
                    ? relationshipTerms
                    : new List<RelationshipTermState>();
                var updatedRelationshipTerms = new List<RelationshipTermState>();
                var deletedRelationshipTermIds = new List<long>();
                var updatedRelationships = new List<RelationshipState>();
                var events = new List<PendingWorldEvent>();
                int activeOpinion = 0;

                foreach (RelationshipTermState term in terms.OrderBy(term => term.Id))
                {
                    if (IsExpired(term, tick))
                    {
                        deletedRelationshipTermIds.Add(term.Id);
                        continue;
                    }

                    RelationshipTermState updatedTerm = DecayTerm(term);
                    if (updatedTerm.Value != term.Value)
                    {
                        updatedRelationshipTerms.Add(updatedTerm);
                    }

                    activeOpinion += updatedTerm.Value;
                }

                int updatedOpinion = ClampOpinion(activeOpinion);
                if (updatedOpinion != currentRelationship.Opinion)
                {
                    var updatedRelationship = new RelationshipState(
                        key,
                        updatedOpinion,
                        currentRelationship.Familiarity,
                        tick
                    );
                    updatedRelationships.Add(updatedRelationship);

                    int opinionChange = updatedOpinion - currentRelationship.Opinion;
                    if (Math.Abs(opinionChange) >= SignificantOpinionChangeThreshold)
                    {
                        events.Add(CreateOpinionChangedEvent(subjectRegionId, key, currentRelationship.Opinion, updatedOpinion, tick));
                    }
                }

                if (updatedRelationshipTerms.Count > 0 || deletedRelationshipTermIds.Count > 0 || updatedRelationships.Count > 0)
                {
                    MutableRelationshipDelta delta = GetOrCreateDelta(deltasByRegion, subjectRegionId, tick);
                    delta.UpdatedRelationshipTerms.AddRange(updatedRelationshipTerms);
                    delta.DeletedRelationshipTermIds.AddRange(deletedRelationshipTermIds);
                    delta.UpdatedRelationships.AddRange(updatedRelationships);
                    delta.Events.AddRange(events);
                }
            }

            return deltasByRegion.Values
                .OrderBy(delta => delta.RegionId.Value)
                .Select(delta => delta.ToRegionStateDelta())
                .ToList();
        }

        private static Dictionary<RelationshipKey, List<RelationshipTermState>> GroupTerms(
            IReadOnlyList<RelationshipTermState> relationshipTerms
        )
        {
            var termsByRelationship = new Dictionary<RelationshipKey, List<RelationshipTermState>>();
            foreach (RelationshipTermState term in relationshipTerms)
            {
                if (!termsByRelationship.TryGetValue(term.Key, out List<RelationshipTermState> terms))
                {
                    terms = new List<RelationshipTermState>();
                    termsByRelationship.Add(term.Key, terms);
                }

                terms.Add(term);
            }

            return termsByRelationship;
        }

        private static MutableRelationshipDelta GetOrCreateDelta(
            Dictionary<long, MutableRelationshipDelta> deltasByRegion,
            RegionId regionId,
            GameTick tick
        )
        {
            if (!deltasByRegion.TryGetValue(regionId.Value, out MutableRelationshipDelta delta))
            {
                delta = new MutableRelationshipDelta(regionId, tick);
                deltasByRegion.Add(regionId.Value, delta);
            }

            return delta;
        }

        private static bool IsExpired(RelationshipTermState term, GameTick tick)
        {
            return term.ExpiresTick.HasValue && term.ExpiresTick.Value.Value <= tick.Value;
        }

        private static RelationshipTermState DecayTerm(RelationshipTermState term)
        {
            if (term.DecayPerTick <= 0 || term.Value == 0)
            {
                return term;
            }

            int decay = Math.Max(1, (int)Math.Ceiling(term.DecayPerTick));
            int value;
            if (term.Value > 0)
            {
                value = Math.Max(0, term.Value - decay);
            }
            else
            {
                value = Math.Min(0, term.Value + decay);
            }

            return new RelationshipTermState(
                term.Id,
                term.Key,
                term.Type,
                value,
                term.DecayPerTick,
                term.ExpiresTick,
                term.PayloadJson
            );
        }

        private static int ClampOpinion(int opinion)
        {
            return Math.Max(-100, Math.Min(100, opinion));
        }

        private static PendingWorldEvent CreateOpinionChangedEvent(
            RegionId regionId,
            RelationshipKey key,
            int previousOpinion,
            int currentOpinion,
            GameTick tick
        )
        {
            string payloadJson = string.Format(
                "{{\"previousOpinion\":{0},\"currentOpinion\":{1}}}",
                previousOpinion,
                currentOpinion
            );
            return new PendingWorldEvent(
                regionId,
                EventType,
                EventSignificance,
                payloadJson,
                tick,
                key.SubjectEntityId,
                key.TargetEntityId
            );
        }

        private sealed class MutableRelationshipDelta
        {
            public MutableRelationshipDelta(RegionId regionId, GameTick tick)
            {
                RegionId = regionId;
                Tick = tick;
            }

            public RegionId RegionId { get; }
            public GameTick Tick { get; }
            public List<PendingWorldEvent> Events { get; } = new List<PendingWorldEvent>();
            public List<RelationshipState> UpdatedRelationships { get; } = new List<RelationshipState>();
            public List<RelationshipTermState> UpdatedRelationshipTerms { get; } = new List<RelationshipTermState>();
            public List<long> DeletedRelationshipTermIds { get; } = new List<long>();

            public RegionStateDelta ToRegionStateDelta()
            {
                return new RegionStateDelta(
                    RegionId,
                    Tick,
                    Events,
                    UpdatedRelationships,
                    UpdatedRelationshipTerms,
                    DeletedRelationshipTermIds
                );
            }
        }
    }
}
