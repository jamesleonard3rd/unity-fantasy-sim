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
    }
}
