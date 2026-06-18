using System.Collections.Generic;
using FantasySim.Content.Definitions;
using FantasySim.Simulation;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Simulation
{
    public sealed class GoalProgressionParserTests
    {
        [Test]
        public void LeafGoalWithoutStepsBecomesSinglePrimitiveStep()
        {
            var goals = new[]
            {
                new GoalDefinition
                {
                    key = "travel_to_region",
                    completionMode = "ordered",
                    rawJson = "{\"goal_type\":\"travel_to_region\",\"completion_mode\":\"ordered\",\"steps\":[]}"
                }
            };

            IReadOnlyDictionary<string, GoalProgressionDefinition> progressions = GoalProgressionParser.Parse(goals);

            GoalProgressionDefinition progression = progressions["travel_to_region"];
            Assert.That(progression.HasSteps, Is.True);
            Assert.That(progression.StepTypes, Is.EqualTo(new[] { "travel_to_region" }));
        }

        [Test]
        public void GoalWithAuthoredStepsKeepsStepTypes()
        {
            var goals = new[]
            {
                new GoalDefinition
                {
                    key = "enter_tournament",
                    completionMode = "ordered",
                    rawJson = "{\"goal_type\":\"enter_tournament\",\"steps\":[{\"goal_type\":\"travel_to_region\"},{\"goal_type\":\"wait_until\"}]}"
                }
            };

            IReadOnlyDictionary<string, GoalProgressionDefinition> progressions = GoalProgressionParser.Parse(goals);

            Assert.That(progressions["enter_tournament"].StepTypes, Is.EqualTo(new[] { "travel_to_region", "wait_until" }));
        }
    }
}
