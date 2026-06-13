using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Content
{
    public sealed class ContentKeyValidatorTests
    {
        [Test]
        public void ValidateUniqueKeysReportsDuplicateKeys()
        {
            var definitions = new[]
            {
                new RaceDefinition { key = "human", displayName = "Human" },
                new RaceDefinition { key = "human", displayName = "Human Again" }
            };

            ContentValidationResult result = ContentKeyValidator.ValidateUniqueKeys(
                definitions,
                definition => definition.key,
                "races"
            );

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Messages.Count, Is.EqualTo(1));
            Assert.That(result.Messages[0].Code, Is.EqualTo("content.duplicate_key"));
        }
    }
}
