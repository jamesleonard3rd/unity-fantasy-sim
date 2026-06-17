using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class StarterWorldSimulationPipelineTests
    {
        private const long EntityIdValue = 10;
        private const long RegionIdValue = 1;
        private const string WaitGoalKey = "wait_goal";

        [Test]
        public void TickMergesGoalScoringStateWithGoalAdvancementPayload()
        {
            var progressions = new Dictionary<string, GoalProgressionDefinition>
            {
                {
                    WaitGoalKey,
                    new GoalProgressionDefinition(WaitGoalKey, "ordered", new[] { GoalAdvancementSystem.WaitUntilStepType })
                }
            };
            var pipeline = new StarterWorldSimulationPipeline(
                goalAdvancementSystem: new GoalAdvancementSystem(progressions)
            );

            LoadedPartitionState state = CreateState(
                new EntityGoalState(
                    1,
                    new EntityId(EntityIdValue),
                    WaitGoalKey,
                    0,
                    GoalScoringSystem.ActiveState,
                    "{\"currentStepIndex\":0,\"targetGameTick\":1}"
                )
            );

            PartitionTickDelta delta = pipeline.Tick(state);

            RegionStateDelta regionDelta = delta.RegionDeltas.Single(region => region.RegionId.Value == RegionIdValue);
            EntityGoalState mergedGoal = regionDelta.UpdatedEntityGoals.Single(goal => goal.Id == 1);

            Assert.That(mergedGoal.State, Is.EqualTo(GoalScoringSystem.ActiveState));
            Assert.That(mergedGoal.Priority, Is.GreaterThan(0));
            Assert.That(mergedGoal.PayloadJson, Does.Contain("\"currentStepIndex\":1"));

            Assert.That(
                regionDelta.Events.Select(worldEvent => worldEvent.Type),
                Does.Contain(GoalAdvancementSystem.StepCompletedEventType)
            );
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
