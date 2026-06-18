using System.Collections.Generic;
using System.Linq;
using FantasySim.Content.Definitions;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using FantasySim.Simulation.Travel;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class GoalAdvancementSystemTravelTests
    {
        private const long EntityIdValue = 10;
        private const string TravelGoalKey = "travel_to_region";

        [Test]
        public void InitializesRouteWithoutMovingOnFirstTick()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), TravelGoalKey, 10, GoalScoringSystem.ActiveState,
                    "{\"currentStepIndex\":0,\"destinationRegionKey\":\"b\"}")
            );
            GoalAdvancementSystem system = CreateSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(0)).Single();

            Assert.That(delta.UpdatedEntityPositions, Is.Empty);
            Assert.That(delta.Events, Is.Empty);
            EntityGoalState updated = delta.UpdatedEntityGoals.Single();
            Assert.That(updated.PayloadJson, Does.Contain("\"routeInitialized\":true"));
            Assert.That(updated.PayloadJson, Does.Contain("\"destinationRegionKey\":\"b\""));
            Assert.That(updated.PayloadJson, Does.Contain("\"nextHopArrivalTick\":5"));
            Assert.That(updated.PayloadJson, Does.Contain("\"currentStepIndex\":0"));
        }

        [Test]
        public void MultiHopRouteInitializationStoresOrderedHops()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), TravelGoalKey, 10, GoalScoringSystem.ActiveState,
                    "{\"currentStepIndex\":0,\"destinationRegionKey\":\"c\"}")
            );
            GoalAdvancementSystem system = CreateSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(0)).Single();

            EntityGoalState updated = delta.UpdatedEntityGoals.Single();
            Assert.That(updated.PayloadJson, Does.Contain("\"routeRegionKeys\":[\"b\",\"c\"]"));
        }

        [Test]
        public void WaitsBetweenHopsUntilArrivalTick()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), TravelGoalKey, 10, GoalScoringSystem.ActiveState,
                    "{\"currentStepIndex\":0,\"destinationRegionKey\":\"b\",\"routeInitialized\":true,\"routeRegionKeys\":[\"b\"],\"currentHopIndex\":0,\"nextHopArrivalTick\":5}")
            );
            GoalAdvancementSystem system = CreateSystem();

            IReadOnlyList<RegionStateDelta> deltas = system.Tick(state, new GameTick(3));

            Assert.That(deltas, Is.Empty);
        }

        [Test]
        public void ArrivesAtRegionAndCompletesSingleHopGoalWhenDue()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), TravelGoalKey, 10, GoalScoringSystem.ActiveState,
                    "{\"currentStepIndex\":0,\"destinationRegionKey\":\"b\",\"routeInitialized\":true,\"routeRegionKeys\":[\"b\"],\"currentHopIndex\":0,\"nextHopArrivalTick\":5}")
            );
            GoalAdvancementSystem system = CreateSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(5)).Single();

            EntityRegionMove move = delta.UpdatedEntityPositions.Single();
            Assert.That(move.EntityId.Value, Is.EqualTo(EntityIdValue));
            Assert.That(move.RegionKey, Is.EqualTo("b"));

            Assert.That(delta.Events.Select(worldEvent => worldEvent.Type), Is.EquivalentTo(new[]
            {
                GoalAdvancementSystem.RegionEnteredEventType,
                GoalAdvancementSystem.StepCompletedEventType,
                GoalAdvancementSystem.GoalCompletedEventType
            }));

            EntityGoalState updated = delta.UpdatedEntityGoals.Single();
            Assert.That(updated.PayloadJson, Does.Contain("\"currentStepIndex\":1"));
        }

        [Test]
        public void CompletesImmediatelyWhenAlreadyAtDestination()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), TravelGoalKey, 10, GoalScoringSystem.ActiveState,
                    "{\"currentStepIndex\":0,\"destinationRegionKey\":\"a\"}")
            );
            GoalAdvancementSystem system = CreateSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(0)).Single();

            Assert.That(delta.UpdatedEntityPositions, Is.Empty);
            Assert.That(delta.Events.Select(worldEvent => worldEvent.Type), Is.EquivalentTo(new[]
            {
                GoalAdvancementSystem.StepCompletedEventType,
                GoalAdvancementSystem.GoalCompletedEventType
            }));
        }

        [Test]
        public void StallsWhenDestinationMissing()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), TravelGoalKey, 10, GoalScoringSystem.ActiveState,
                    "{\"currentStepIndex\":0}")
            );
            GoalAdvancementSystem system = CreateSystem();

            IReadOnlyList<RegionStateDelta> deltas = system.Tick(state, new GameTick(100));

            Assert.That(deltas, Is.Empty);
        }

        private static GoalAdvancementSystem CreateSystem()
        {
            var progressions = new Dictionary<string, GoalProgressionDefinition>
            {
                {
                    TravelGoalKey,
                    new GoalProgressionDefinition(TravelGoalKey, "ordered", new[] { GoalAdvancementSystem.TravelStepType })
                }
            };

            TravelGraph graph = TravelGraph.Build(
                new[]
                {
                    new TravelEdgeDefinition { fromKey = "a", toKey = "b", mode = "road", baseTicks = 5, bidirectional = true },
                    new TravelEdgeDefinition { fromKey = "b", toKey = "c", mode = "road", baseTicks = 5, bidirectional = true }
                },
                System.Array.Empty<RegionDefinition>()
            );

            return new GoalAdvancementSystem(progressions, graph);
        }

        private static LoadedPartitionState CreateState(EntityGoalState goal)
        {
            return new LoadedPartitionState(
                new TickContext(new GameTick(0), 24, 1, "starter_world", "Starter World", 180f, new GameTick(0)),
                new[]
                {
                    CreateRegion(1, "a"),
                    CreateRegion(2, "b"),
                    CreateRegion(3, "c")
                },
                new[] { CreateEntity(new RegionId(1)) },
                new RelationshipState[0],
                new RelationshipTermState[0],
                new[] { goal }
            );
        }

        private static RegionState CreateRegion(long id, string key)
        {
            return new RegionState(
                new RegionId(id),
                key,
                key,
                "region",
                null,
                180f,
                new GameTick(0),
                "coarse",
                null,
                null
            );
        }

        private static EntityState CreateEntity(RegionId regionId)
        {
            return new EntityState(
                new EntityId(EntityIdValue),
                "hero",
                "Hero",
                "human",
                "standard_human",
                0,
                true,
                "{}",
                regionId,
                null,
                null,
                null,
                null
            );
        }
    }
}
