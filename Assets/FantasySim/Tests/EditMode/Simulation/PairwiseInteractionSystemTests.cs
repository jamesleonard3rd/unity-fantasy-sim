using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class PairwiseInteractionSystemTests
    {
        private const long RegionA = 1;
        private const long RegionB = 2;

        [Test]
        public void TickProducesOneInteractionPerColocatedEntity()
        {
            LoadedPartitionState state = CreateState(
                new[] { CreateEntity(10, RegionA), CreateEntity(20, RegionA) },
                new RelationshipState[0]
            );
            var system = new PairwiseInteractionSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(5)).Single();

            Assert.That(delta.UpdatedRelationshipTerms, Has.Count.EqualTo(2));
            Assert.That(delta.Events, Has.Count.EqualTo(2));
            Assert.That(delta.Events.Select(worldEvent => worldEvent.Type), Is.All.EqualTo(PairwiseInteractionSystem.EventType));

            RelationshipTermState fromSubject = delta.UpdatedRelationshipTerms
                .Single(term => term.Key.SubjectEntityId.Value == 10);
            Assert.That(fromSubject.Key.TargetEntityId.Value, Is.EqualTo(20));
            Assert.That(fromSubject.Type, Is.EqualTo(PairwiseInteractionSystem.InteractionTermType));
            Assert.That(fromSubject.Id, Is.EqualTo(0));
            Assert.That(fromSubject.ExpiresTick.HasValue, Is.True);
            Assert.That(fromSubject.ExpiresTick.Value.Value, Is.GreaterThan(5));
        }

        [Test]
        public void TickDoesNotInteractAcrossRegions()
        {
            LoadedPartitionState state = CreateState(
                new[] { CreateEntity(10, RegionA), CreateEntity(20, RegionB) },
                new RelationshipState[0]
            );
            var system = new PairwiseInteractionSystem();

            IReadOnlyList<RegionStateDelta> deltas = system.Tick(state, new GameTick(5));

            Assert.That(deltas, Is.Empty);
        }

        [Test]
        public void TickPrefersCandidateWithExistingRelationship()
        {
            var relationships = new[]
            {
                new RelationshipState(new RelationshipKey(new EntityId(10), new EntityId(30)), 50, 0, new GameTick(0))
            };
            LoadedPartitionState state = CreateState(
                new[] { CreateEntity(10, RegionA), CreateEntity(20, RegionA), CreateEntity(30, RegionA) },
                relationships
            );
            var system = new PairwiseInteractionSystem(candidateCap: 30, maxInteractionsPerEntity: 1);

            RegionStateDelta delta = system.Tick(state, new GameTick(5)).Single();

            RelationshipTermState fromSubject = delta.UpdatedRelationshipTerms
                .Single(term => term.Key.SubjectEntityId.Value == 10);
            Assert.That(fromSubject.Key.TargetEntityId.Value, Is.EqualTo(30));
        }

        [Test]
        public void TickRespectsMaxInteractionsPerEntity()
        {
            LoadedPartitionState state = CreateState(
                new[]
                {
                    CreateEntity(10, RegionA),
                    CreateEntity(20, RegionA),
                    CreateEntity(30, RegionA)
                },
                new RelationshipState[0]
            );
            var system = new PairwiseInteractionSystem(candidateCap: 30, maxInteractionsPerEntity: 1);

            RegionStateDelta delta = system.Tick(state, new GameTick(5)).Single();

            foreach (long subjectId in new long[] { 10, 20, 30 })
            {
                Assert.That(
                    delta.UpdatedRelationshipTerms.Count(term => term.Key.SubjectEntityId.Value == subjectId),
                    Is.EqualTo(1)
                );
            }
        }

        [Test]
        public void TickProducesNothingWhenInteractionsDisabled()
        {
            LoadedPartitionState state = CreateState(
                new[] { CreateEntity(10, RegionA), CreateEntity(20, RegionA) },
                new RelationshipState[0]
            );
            var system = new PairwiseInteractionSystem(maxInteractionsPerEntity: 0);

            IReadOnlyList<RegionStateDelta> deltas = system.Tick(state, new GameTick(5));

            Assert.That(deltas, Is.Empty);
        }

        private static LoadedPartitionState CreateState(EntityState[] entities, RelationshipState[] relationships)
        {
            return new LoadedPartitionState(
                new TickContext(new GameTick(0), 24, 1, "starter_world", "Starter World", 180f, new GameTick(0)),
                new[] { CreateRegion(RegionA), CreateRegion(RegionB) },
                entities,
                relationships,
                new RelationshipTermState[0],
                new EntityGoalState[0]
            );
        }

        private static RegionState CreateRegion(long id)
        {
            return new RegionState(
                new RegionId(id),
                $"region_{id}",
                $"Region {id}",
                "region",
                null,
                180f,
                new GameTick(0),
                "coarse",
                null,
                null
            );
        }

        private static EntityState CreateEntity(long id, long regionId)
        {
            return new EntityState(
                new EntityId(id),
                $"entity_{id}",
                $"Entity {id}",
                "human",
                "standard_human",
                0,
                true,
                "{}",
                new RegionId(regionId),
                null,
                null,
                null,
                null
            );
        }
    }
}
