using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class GoalAdvancementSystemTests
    {
        private const long EntityIdValue = 10;
        private const long RegionIdValue = 1;
        private const string WaitGoalKey = "wait_goal";
        private const string MultiStepGoalKey = "multi_step_goal";
        private const string DeferredGoalKey = "deferred_goal";

        [Test]
        public void TickDoesNotAdvanceWaitUntilStepBeforeTargetTick()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), WaitGoalKey, 5, GoalScoringSystem.ActiveState, "{\"currentStepIndex\":0,\"targetGameTick\":100}")
            );
            GoalAdvancementSystem system = CreateSystem();

            IReadOnlyList<RegionStateDelta> deltas = system.Tick(state, new GameTick(50));

            Assert.That(deltas, Is.Empty);
        }

        [Test]
        public void TickAdvancesWaitUntilStepAndCompletesSingleStepGoal()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), WaitGoalKey, 5, GoalScoringSystem.ActiveState, "{\"currentStepIndex\":0,\"targetGameTick\":100}")
            );
            GoalAdvancementSystem system = CreateSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(100)).Single();

            EntityGoalState updated = delta.UpdatedEntityGoals.Single();
            Assert.That(updated.Id, Is.EqualTo(1));
            Assert.That(updated.PayloadJson, Does.Contain("\"currentStepIndex\":1"));

            Assert.That(delta.Events.Select(worldEvent => worldEvent.Type), Is.EquivalentTo(new[]
            {
                GoalAdvancementSystem.StepCompletedEventType,
                GoalAdvancementSystem.GoalCompletedEventType
            }));
            Assert.That(delta.Events, Has.All.Matches<PendingWorldEvent>(worldEvent =>
                worldEvent.SubjectEntityId.HasValue && worldEvent.SubjectEntityId.Value.Value == EntityIdValue));
        }

        [Test]
        public void TickDoesNotAdvanceDeferredStepType()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), DeferredGoalKey, 5, GoalScoringSystem.ActiveState, "{\"currentStepIndex\":0}")
            );
            GoalAdvancementSystem system = CreateSystem();

            IReadOnlyList<RegionStateDelta> deltas = system.Tick(state, new GameTick(100));

            Assert.That(deltas, Is.Empty);
        }

        [Test]
        public void TickAdvancesOneOrderedStepPerTickWithoutCompletingRemainingSteps()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), MultiStepGoalKey, 5, GoalScoringSystem.ActiveState, "{\"currentStepIndex\":0,\"targetGameTick\":10}")
            );
            GoalAdvancementSystem system = CreateSystem();

            RegionStateDelta delta = system.Tick(state, new GameTick(10)).Single();

            EntityGoalState updated = delta.UpdatedEntityGoals.Single();
            Assert.That(updated.PayloadJson, Does.Contain("\"currentStepIndex\":1"));
            Assert.That(delta.Events.Select(worldEvent => worldEvent.Type), Is.EqualTo(new[]
            {
                GoalAdvancementSystem.StepCompletedEventType
            }));
        }

        [Test]
        public void TickIgnoresInactiveGoals()
        {
            LoadedPartitionState state = CreateState(
                new EntityGoalState(1, new EntityId(EntityIdValue), WaitGoalKey, 5, GoalScoringSystem.InactiveState, "{\"currentStepIndex\":0,\"targetGameTick\":1}")
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
                    WaitGoalKey,
                    new GoalProgressionDefinition(WaitGoalKey, "ordered", new[] { GoalAdvancementSystem.WaitUntilStepType })
                },
                {
                    MultiStepGoalKey,
                    new GoalProgressionDefinition(MultiStepGoalKey, "ordered", new[]
                    {
                        GoalAdvancementSystem.WaitUntilStepType,
                        "compete_in_tournament"
                    })
                },
                {
                    DeferredGoalKey,
                    new GoalProgressionDefinition(DeferredGoalKey, "ordered", new[] { "travel_to_region" })
                }
            };

            return new GoalAdvancementSystem(progressions);
        }

        private static LoadedPartitionState CreateState(EntityGoalState goal)
        {
            return new LoadedPartitionState(
                new TickContext(new GameTick(0), 24, 1, "starter_world", "Starter World", 180f, new GameTick(0)),
                new[] { CreateRegion() },
                new[] { CreateEntity() },
                new RelationshipState[0],
                new RelationshipTermState[0],
                new[] { goal }
            );
        }

        private static RegionState CreateRegion()
        {
            return new RegionState(
                new RegionId(RegionIdValue),
                "region",
                "Region",
                "region",
                null,
                180f,
                new GameTick(0),
                "coarse",
                null,
                null
            );
        }

        private static EntityState CreateEntity()
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
                new RegionId(RegionIdValue),
                null,
                null,
                null,
                null
            );
        }
    }
}
