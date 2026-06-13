using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Results;

namespace FantasySim.Content.Validation
{
    public sealed class ContentValidationResult
    {
        public ContentValidationResult(IEnumerable<ValidationMessage> messages)
        {
            Messages = messages.ToArray();
        }

        public IReadOnlyList<ValidationMessage> Messages { get; }

        public bool IsValid => !Messages.Any(message => message.Severity == ValidationSeverity.Error);

        public static ContentValidationResult Success { get; } =
            new ContentValidationResult(System.Array.Empty<ValidationMessage>());
    }
}
