using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class RegionHeartbeatSystemTests
    {
        [Test]
        public void TickAppendsLowSignificanceRegionTickEvent()
        {
            var system = new RegionHeartbeatSystem();
            RegionState region = CreateRegion(42);
            var tick = new GameTick(7);

            RegionStateDelta delta = system.Tick(region, tick);

            Assert.That(delta.RegionId.Value, Is.EqualTo(region.Id.Value));
            Assert.That(delta.Tick.Value, Is.EqualTo(tick.Value));
            Assert.That(delta.Events, Has.Count.EqualTo(1));

            PendingWorldEvent worldEvent = delta.Events.Single();
            Assert.That(worldEvent.RegionId.HasValue, Is.True);
            Assert.That(worldEvent.RegionId.Value.Value, Is.EqualTo(region.Id.Value));
            Assert.That(worldEvent.Type, Is.EqualTo(RegionHeartbeatSystem.EventType));
            Assert.That(worldEvent.Significance, Is.EqualTo(RegionHeartbeatSystem.EventSignificance));
            Assert.That(worldEvent.PayloadJson, Is.EqualTo(RegionHeartbeatSystem.EventPayloadJson));
            Assert.That(worldEvent.OccurredTick.Value, Is.EqualTo(tick.Value));
            Assert.That(worldEvent.SubjectEntityId.HasValue, Is.False);
            Assert.That(worldEvent.TargetEntityId.HasValue, Is.False);
        }

        [Test]
        public void PipelineRunsHeartbeatForEachLoadedRegionAtNextTick()
        {
            var pipeline = new StarterWorldSimulationPipeline();
            var state = new LoadedPartitionState(
                new TickContext(new GameTick(3), 24, 1, "starter_world", "Starter World", 180f, new GameTick(0)),
                new[] { CreateRegion(1), CreateRegion(2) },
                new EntityState[0]
            );

            PartitionTickDelta delta = pipeline.Tick(state);

            Assert.That(delta.PreviousTick.Value, Is.EqualTo(3));
            Assert.That(delta.TargetTick.Value, Is.EqualTo(4));
            Assert.That(delta.RegionDeltas, Has.Count.EqualTo(2));
            Assert.That(delta.EventCount, Is.EqualTo(2));
            Assert.That(delta.RegionDeltas.Select(regionDelta => regionDelta.Tick.Value), Is.All.EqualTo(4));
            Assert.That(
                delta.RegionDeltas.SelectMany(regionDelta => regionDelta.Events).Select(worldEvent => worldEvent.Type),
                Is.All.EqualTo(RegionHeartbeatSystem.EventType)
            );
        }

        [Test]
        public void PipelineRunsRelationshipDynamicsBeforeHeartbeatAndMergesRegionDelta()
        {
            var key = new RelationshipKey(new EntityId(10), new EntityId(20));
            var pipeline = new StarterWorldSimulationPipeline();
            var state = new LoadedPartitionState(
                new TickContext(new GameTick(3), 24, 1, "starter_world", "Starter World", 180f, new GameTick(0)),
                new[] { CreateRegion(1) },
                new[]
                {
                    CreateEntity(10, "subject", 1),
                    CreateEntity(20, "target", 1)
                },
                new[] { new RelationshipState(key, 0, 0, new GameTick(3)) },
                new[] { new RelationshipTermState(1, key, "favor", 15, 0.0, null, "{}") }
            );

            PartitionTickDelta delta = pipeline.Tick(state);

            RegionStateDelta regionDelta = delta.RegionDeltas.Single();
            Assert.That(regionDelta.UpdatedRelationships.Single().Opinion, Is.EqualTo(15));
            Assert.That(regionDelta.Events, Has.Count.EqualTo(2));
            Assert.That(regionDelta.Events[0].Type, Is.EqualTo(RelationshipDynamicsSystem.EventType));
            Assert.That(regionDelta.Events[1].Type, Is.EqualTo(RegionHeartbeatSystem.EventType));
            Assert.That(delta.EventCount, Is.EqualTo(2));
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
