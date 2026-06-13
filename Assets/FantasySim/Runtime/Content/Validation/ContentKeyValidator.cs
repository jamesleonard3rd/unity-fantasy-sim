using System;
using System.Collections.Generic;
using FantasySim.Core.Results;

namespace FantasySim.Content.Validation
{
    public static class ContentKeyValidator
    {
        public static ContentValidationResult ValidateUniqueKeys<TDefinition>(
            IEnumerable<TDefinition> definitions,
            Func<TDefinition, string> keySelector,
            string contentGroupName
        )
        {
            var messages = new List<ValidationMessage>();
            var seenKeys = new HashSet<string>();

            foreach (TDefinition definition in definitions)
            {
                string key = keySelector(definition);
                if (string.IsNullOrWhiteSpace(key))
                {
                    messages.Add(new ValidationMessage(
                        ValidationSeverity.Error,
                        "content.missing_key",
                        $"{contentGroupName} contains a definition without a key."
                    ));
                    continue;
                }

                if (!seenKeys.Add(key))
                {
                    messages.Add(new ValidationMessage(
                        ValidationSeverity.Error,
                        "content.duplicate_key",
                        $"{contentGroupName} contains duplicate key '{key}'."
                    ));
                }
            }

            return new ContentValidationResult(messages);
        }

        public static ContentValidationResult ValidateRequiredDisplayNames<TDefinition>(
            IEnumerable<TDefinition> definitions,
            Func<TDefinition, string> displayNameSelector,
            string contentGroupName
        )
        {
            var messages = new List<ValidationMessage>();

            foreach (TDefinition definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(displayNameSelector(definition)))
                {
                    messages.Add(new ValidationMessage(
                        ValidationSeverity.Error,
                        "content.missing_display_name",
                        $"{contentGroupName} contains a definition without a display name."
                    ));
                }
            }

            return new ContentValidationResult(messages);
        }

        public static ContentValidationResult ValidateUnambiguousNormalizedDisplayNames<TDefinition>(
            IEnumerable<TDefinition> definitions,
            Func<TDefinition, string> displayNameSelector,
            string contentGroupName
        )
        {
            var messages = new List<ValidationMessage>();
            var firstDisplayNameByNormalizedName = new Dictionary<string, string>();
            var reportedNormalizedNames = new HashSet<string>();

            foreach (TDefinition definition in definitions)
            {
                string displayName = displayNameSelector(definition);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                string normalizedName = ContentKeyNormalizer.Normalize(displayName);
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    continue;
                }

                if (!firstDisplayNameByNormalizedName.TryGetValue(normalizedName, out string firstDisplayName))
                {
                    firstDisplayNameByNormalizedName[normalizedName] = displayName;
                    continue;
                }

                if (reportedNormalizedNames.Add(normalizedName))
                {
                    messages.Add(new ValidationMessage(
                        ValidationSeverity.Error,
                        "content.ambiguous_display_name",
                        $"{contentGroupName} contains display names '{firstDisplayName}' and '{displayName}' that both normalize to '{normalizedName}'."
                    ));
                }
            }

            return new ContentValidationResult(messages);
        }
    }
}
