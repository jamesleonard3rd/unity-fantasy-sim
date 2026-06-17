using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class GoalScoringSystemTests
    {
        private const long EntityId = 10;
        private const long RegionId = 1;

        [Test]
        public void TickSelectsAmbitiousRolePrimaryGoalAndEmitsEvent()
        {
            LoadedPartitionState state = CreateState(
                "{\"role\":\"patriarch\",\"traitKeys\":[]}",
                new RelationshipState[0],
                DefaultGoals()
            );
            var system = new GoalScoringSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(1)).Single();

            Assert.That(delta.UpdatedEntityGoals, Has.Count.EqualTo(3));
            EntityGoalState primary = delta.UpdatedEntityGoals.Single(goal => goal.State == GoalScoringSystem.ActiveState);
            Assert.That(primary.GoalKey, Is.EqualTo("join_faction"));
            Assert.That(primary.Priority, Is.EqualTo(60));
            Assert.That(
                delta.UpdatedEntityGoals.Where(goal => goal.GoalKey != "join_faction").Select(goal => goal.State),
                Is.All.EqualTo(GoalScoringSystem.InactiveState)
            );

            PendingWorldEvent worldEvent = delta.Events.Single();
            Assert.That(worldEvent.Type, Is.EqualTo(GoalScoringSystem.EventType));
            Assert.That(worldEvent.Significance, Is.EqualTo(GoalScoringSystem.EventSignificance));
            Assert.That(worldEvent.SubjectEntityId.HasValue, Is.True);
            Assert.That(worldEvent.SubjectEntityId.Value.Value, Is.EqualTo(EntityId));
            Assert.That(worldEvent.PayloadJson, Is.EqualTo("{\"goalKey\":\"join_faction\"}"));
        }

        [Test]
        public void TickPrefersTournamentForCombatTraitsWhenRoleIsNotAmbitious()
        {
            LoadedPartitionState state = CreateState(
                "{\"role\":\"member\",\"traitKeys\":[\"strong\",\"tough\",\"quick\"]}",
                new RelationshipState[0],
                DefaultGoals()
            );
            var system = new GoalScoringSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(1)).Single();

            EntityGoalState primary = delta.UpdatedEntityGoals.Single(goal => goal.State == GoalScoringSystem.ActiveState);
            Assert.That(primary.GoalKey, Is.EqualTo("enter_tournament"));
            Assert.That(primary.Priority, Is.EqualTo(60));
        }

        [Test]
        public void TickAddsOutgoingPositiveRelationshipSignalToJoinFaction()
        {
            var firstLink = new RelationshipKey(new EntityId(EntityId), new EntityId(20));
            var secondLink = new RelationshipKey(new EntityId(EntityId), new EntityId(30));
            LoadedPartitionState state = CreateState(
                "{\"role\":\"member\",\"traitKeys\":[]}",
                new[]
                {
                    new RelationshipState(firstLink, 40, 0, new GameTick(0)),
                    new RelationshipState(secondLink, 25, 0, new GameTick(0))
                },
                DefaultGoals()
            );
            var system = new GoalScoringSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(1)).Single();

            EntityGoalState primary = delta.UpdatedEntityGoals.Single(goal => goal.State == GoalScoringSystem.ActiveState);
            Assert.That(primary.GoalKey, Is.EqualTo("join_faction"));
            Assert.That(primary.Priority, Is.EqualTo(24));
        }

        [Test]
        public void TickProducesNoDeltaWhenPrimaryGoalIsUnchanged()
        {
            LoadedPartitionState state = CreateState(
                "{\"role\":\"patriarch\",\"traitKeys\":[]}",
                new RelationshipState[0],
                new[]
                {
                    new EntityGoalState(1, new EntityId(EntityId), "travel_to_region", 10, GoalScoringSystem.InactiveState, "{}"),
                    new EntityGoalState(2, new EntityId(EntityId), "join_faction", 60, GoalScoringSystem.ActiveState, "{}"),
                    new EntityGoalState(3, new EntityId(EntityId), "enter_tournament", 15, GoalScoringSystem.InactiveState, "{}")
                }
            );
            var system = new GoalScoringSystem();

            IReadOnlyList<RegionStateDelta> deltas = system.Tick(state, new GameTick(1));

            Assert.That(deltas, Is.Empty);
        }

        private static EntityGoalState[] DefaultGoals()
        {
            return new[]
            {
                new EntityGoalState(1, new EntityId(EntityId), "travel_to_region", 0, GoalScoringSystem.InactiveState, "{}"),
                new EntityGoalState(2, new EntityId(EntityId), "join_faction", 0, GoalScoringSystem.InactiveState, "{}"),
                new EntityGoalState(3, new EntityId(EntityId), "enter_tournament", 0, GoalScoringSystem.InactiveState, "{}")
            };
        }

        private static LoadedPartitionState CreateState(
            string entityPayloadJson,
            RelationshipState[] relationships,
            EntityGoalState[] entityGoals
        )
        {
            return new LoadedPartitionState(
                new TickContext(new GameTick(0), 24, 1, "starter_world", "Starter World", 180f, new GameTick(0)),
                new[] { CreateRegion(RegionId) },
                new[] { CreateEntity(EntityId, entityPayloadJson, RegionId) },
                relationships,
                new RelationshipTermState[0],
                entityGoals
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

        private static EntityState CreateEntity(long id, string payloadJson, long regionId)
        {
            return new EntityState(
                new EntityId(id),
                $"entity_{id}",
                $"Entity {id}",
                "human",
                "standard_human",
                0,
                true,
                payloadJson,
                new RegionId(regionId),
                null,
                null,
                null,
                null
            );
        }
    }
}
