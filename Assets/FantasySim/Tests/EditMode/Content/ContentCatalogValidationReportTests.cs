using System;
using System.Collections.Generic;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using FantasySim.Core.Results;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Content
{
    public sealed class ContentCatalogValidationReportTests
    {
        [Test]
        public void FormatSummaryIncludesCatalogCountsAndValidationMessageCount()
        {
            var catalog = new ContentCatalog(
                new Dictionary<string, FactionDefinition> { { "house_valemont", new FactionDefinition() } },
                new Dictionary<string, RaceDefinition> { { "human", new RaceDefinition() } },
                new Dictionary<string, StatDefinition> { { "strength", new StatDefinition() } },
                new Dictionary<string, TraitDefinition> { { "strong", new TraitDefinition() } },
                new Dictionary<string, RegionDefinition> { { "valemont_keep", new RegionDefinition() } },
                new Dictionary<string, EntityTemplateDefinition> { { "hero", new EntityTemplateDefinition() } }
            );
            var result = new ContentCatalogBuildResult(
                catalog,
                new ContentValidationResult(Array.Empty<ValidationMessage>())
            );

            string summary = ContentCatalogValidationReport.FormatSummary(result);

            Assert.That(summary, Does.Contain("Fantasy Sim game data is valid."));
            Assert.That(summary, Does.Contain("- Factions: 1"));
            Assert.That(summary, Does.Contain("- Entity templates: 1"));
            Assert.That(summary, Does.Contain("Validation messages: 0"));
        }

        [Test]
        public void FormatMessageIncludesSeverityCodeAndMessage()
        {
            var message = new ValidationMessage(
                ValidationSeverity.Error,
                "content.invalid_reference",
                "entity templates 'hero' references missing regionKey 'missing_region'."
            );

            string formatted = ContentCatalogValidationReport.FormatMessage(message);

            Assert.That(formatted, Is.EqualTo("[Error] content.invalid_reference: entity templates 'hero' references missing regionKey 'missing_region'."));
        }
    }
}
