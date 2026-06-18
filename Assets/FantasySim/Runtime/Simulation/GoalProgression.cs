using System;
using System.Collections.Generic;
using FantasySim.Content.Definitions;
using UnityEngine;

namespace FantasySim.Simulation
{
    public sealed class GoalProgressionDefinition
    {
        public GoalProgressionDefinition(string goalKey, string completionMode, IReadOnlyList<string> stepTypes)
        {
            GoalKey = goalKey;
            CompletionMode = completionMode;
            StepTypes = stepTypes ?? new List<string>();
        }

        public string GoalKey { get; }
        public string CompletionMode { get; }
        public IReadOnlyList<string> StepTypes { get; }

        public bool HasSteps => StepTypes.Count > 0;
    }

    public static class GoalProgressionParser
    {
        public static IReadOnlyDictionary<string, GoalProgressionDefinition> Parse(IEnumerable<GoalDefinition> goals)
        {
            var progressions = new Dictionary<string, GoalProgressionDefinition>(StringComparer.Ordinal);
            if (goals == null)
            {
                return progressions;
            }

            foreach (GoalDefinition goal in goals)
            {
                if (goal == null || string.IsNullOrWhiteSpace(goal.key))
                {
                    continue;
                }

                IReadOnlyList<string> stepTypes = ParseStepTypes(goal.rawJson);
                if (stepTypes.Count == 0)
                {
                    // Leaf goals (no authored substeps) act as a single primitive step equal to
                    // their own type, so the goal advances through the matching step handler.
                    stepTypes = new[] { goal.key };
                }

                progressions[goal.key] = new GoalProgressionDefinition(
                    goal.key,
                    goal.completionMode,
                    stepTypes
                );
            }

            return progressions;
        }

        private static IReadOnlyList<string> ParseStepTypes(string rawJson)
        {
            var stepTypes = new List<string>();
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return stepTypes;
            }

            GoalStepsWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<GoalStepsWrapper>(rawJson);
            }
            catch (ArgumentException)
            {
                return stepTypes;
            }

            if (wrapper?.steps == null)
            {
                return stepTypes;
            }

            foreach (GoalStepJson step in wrapper.steps)
            {
                if (step != null && !string.IsNullOrWhiteSpace(step.goal_type))
                {
                    stepTypes.Add(step.goal_type);
                }
            }

            return stepTypes;
        }

        [Serializable]
        private sealed class GoalStepsWrapper
        {
            public GoalStepJson[] steps;
        }

        [Serializable]
        private sealed class GoalStepJson
        {
            public string goal_type;
        }
    }
}
