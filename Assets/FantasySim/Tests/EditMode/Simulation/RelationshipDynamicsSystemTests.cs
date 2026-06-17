using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class RelationshipDynamicsSystemTests
    {
        [Test]
        public void TickDecaysTemporaryTermsPreservesPermanentTermsAndRecalculatesOpinion()
        {
            var key = new RelationshipKey(new EntityId(10), new EntityId(20));
            LoadedPartitionState state = CreateState(
                new[] { new RelationshipState(key, 10, 3, new GameTick(0)) },
                new[]
                {
                    new RelationshipTermState(1, key, "favor", 10, 2.0, null, "{}"),
                    new RelationshipTermState(2, key, "kinship", -3, 0.0, null, "{}")
                }
            );
            var system = new RelationshipDynamicsSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(1)).Single();

            RelationshipTermState decayedTerm = delta.UpdatedRelationshipTerms.Single();
            Assert.That(decayedTerm.Id, Is.EqualTo(1));
            Assert.That(decayedTerm.Value, Is.EqualTo(8));
            Assert.That(delta.DeletedRelationshipTermIds, Is.Empty);

            RelationshipState relationship = delta.UpdatedRelationships.Single();
            Assert.That(relationship.Opinion, Is.EqualTo(5));
            Assert.That(relationship.Familiarity, Is.EqualTo(3));
            Assert.That(relationship.UpdatedTick.Value, Is.EqualTo(1));
            Assert.That(delta.Events, Is.Empty);
        }

        [Test]
        public void TickDeletesExpiredTerms()
        {
            var key = new RelationshipKey(new EntityId(10), new EntityId(20));
            LoadedPartitionState state = CreateState(
                new[] { new RelationshipState(key, 5, 0, new GameTick(0)) },
                new[]
                {
                    new RelationshipTermState(7, key, "favor", 5, 0.0, new GameTick(1), "{}")
                }
            );
            var system = new RelationshipDynamicsSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(1)).Single();

            Assert.That(delta.DeletedRelationshipTermIds, Is.EqualTo(new[] { 7L }));
            Assert.That(delta.UpdatedRelationshipTerms, Is.Empty);
            Assert.That(delta.UpdatedRelationships.Single().Opinion, Is.EqualTo(0));
        }

        [Test]
        public void TickClampsOpinionAndEmitsEventForSignificantChange()
        {
            var key = new RelationshipKey(new EntityId(10), new EntityId(20));
            LoadedPartitionState state = CreateState(
                new[] { new RelationshipState(key, 0, 0, new GameTick(0)) },
                new[]
                {
                    new RelationshipTermState(1, key, "loyalty", 120, 0.0, null, "{}")
                }
            );
            var system = new RelationshipDynamicsSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(1)).Single();

            Assert.That(delta.UpdatedRelationships.Single().Opinion, Is.EqualTo(100));
            PendingWorldEvent worldEvent = delta.Events.Single();
            Assert.That(worldEvent.Type, Is.EqualTo(RelationshipDynamicsSystem.EventType));
            Assert.That(worldEvent.Significance, Is.EqualTo(RelationshipDynamicsSystem.EventSignificance));
            Assert.That(worldEvent.SubjectEntityId.HasValue, Is.True);
            Assert.That(worldEvent.SubjectEntityId.Value.Value, Is.EqualTo(10));
            Assert.That(worldEvent.TargetEntityId.HasValue, Is.True);
            Assert.That(worldEvent.TargetEntityId.Value.Value, Is.EqualTo(20));
            Assert.That(worldEvent.PayloadJson, Is.EqualTo("{\"previousOpinion\":0,\"currentOpinion\":100}"));
        }

        [Test]
        public void TickDoesNotEmitEventForSmallOpinionDrift()
        {
            var key = new RelationshipKey(new EntityId(10), new EntityId(20));
            LoadedPartitionState state = CreateState(
                new[] { new RelationshipState(key, 5, 0, new GameTick(0)) },
                new[]
                {
                    new RelationshipTermState(1, key, "favor", 6, 0.0, null, "{}")
                }
            );
            var system = new RelationshipDynamicsSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(1)).Single();

            Assert.That(delta.UpdatedRelationships.Single().Opinion, Is.EqualTo(6));
            Assert.That(delta.Events, Is.Empty);
        }

        private static LoadedPartitionState CreateState(
            RelationshipState[] relationships,
            RelationshipTermState[] relationshipTerms
        )
        {
            return new LoadedPartitionState(
                new TickContext(new GameTick(0), 24, 1, "starter_world", "Starter World", 180f, new GameTick(0)),
                new[] { CreateRegion(1) },
                new[]
                {
                    CreateEntity(10, "subject", 1),
                    CreateEntity(20, "target", 1)
                },
                relationships,
                relationshipTerms
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

        private static EntityState CreateEntity(long id, string templateKey, long regionId)
        {
            return new EntityState(
                new EntityId(id),
                templateKey,
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
