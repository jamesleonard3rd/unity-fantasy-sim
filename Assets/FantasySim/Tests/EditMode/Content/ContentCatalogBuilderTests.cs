using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Loaders;
using FantasySim.Core.Results;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Content
{
    public sealed class ContentCatalogBuilderTests
    {
        [Test]
        public void BuildPopulatesCatalogFromRealGameData()
        {
            ContentCatalogBuildResult result = new ContentCatalogBuilder().Build();

            Assert.That(result.Catalog.Factions.ContainsKey("the_white_tower"), Is.True);
            Assert.That(result.Catalog.Races.ContainsKey("human"), Is.True);
            Assert.That(result.Catalog.Stats.ContainsKey("strength"), Is.True);
            Assert.That(result.Catalog.Traits.ContainsKey("strong"), Is.True);
            Assert.That(result.Catalog.Regions.ContainsKey("whisperwood_marches"), Is.True);
            Assert.That(result.Catalog.EntityTemplates.ContainsKey("adrien_valemont"), Is.True);
            Assert.That(result.ValidationResult.Messages.All(message => !string.IsNullOrWhiteSpace(message.Message)), Is.True);
        }

        [Test]
        public void BuildReportsInvalidCrossContentReferences()
        {
            var builder = new ContentCatalogBuilder(
                new StaticContentLoader<FactionDefinition>(new[]
                {
                    new FactionDefinition
                    {
                        key = "house_valemont",
                        displayName = "House Valemont",
                        parentKey = "missing_parent"
                    }
                }),
                new StaticContentLoader<RaceDefinition>(new[]
                {
                    new RaceDefinition { key = "human", displayName = "Human" }
                }),
                new StaticContentLoader<StatDefinition>(new[]
                {
                    new StatDefinition { key = "strength", displayName = "Strength" }
                }),
                new StaticContentLoader<TraitDefinition>(new[]
                {
                    new TraitDefinition
                    {
                        key = "strong",
                        displayName = "Strong",
                        modifiers = new[]
                        {
                            new TraitModifierDefinition { statKey = "missing_stat", type = "add", value = 1 }
                        }
                    }
                }),
                new StaticContentLoader<RegionDefinition>(new[]
                {
                    new RegionDefinition
                    {
                        key = "valemont_keep",
                        displayName = "Valemont Keep",
                        parentKey = "missing_region",
                        tickIntervalSeconds = 180f
                    }
                }),
                new StaticContentLoader<EntityTemplateDefinition>(new[]
                {
                    new EntityTemplateDefinition
                    {
                        key = "hero",
                        displayName = "Hero",
                        raceKey = "missing_race",
                        houseKey = "missing_house",
                        regionKey = "missing_region",
                        traitKeys = new[] { "missing_trait" },
                        factions = new[]
                        {
                            new EntityFactionMembershipDefinition { factionKey = "missing_faction", rank = "member" }
                        }
                    }
                })
            );

            ContentCatalogBuildResult result = builder.Build();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ValidationResult.Messages.Count(message => message.Code == "content.invalid_reference"), Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void BuildReportsMissingRequiredEntityReferences()
        {
            var builder = new ContentCatalogBuilder(
                new StaticContentLoader<FactionDefinition>(Array.Empty<FactionDefinition>()),
                new StaticContentLoader<RaceDefinition>(Array.Empty<RaceDefinition>()),
                new StaticContentLoader<StatDefinition>(Array.Empty<StatDefinition>()),
                new StaticContentLoader<TraitDefinition>(Array.Empty<TraitDefinition>()),
                new StaticContentLoader<RegionDefinition>(Array.Empty<RegionDefinition>()),
                new StaticContentLoader<EntityTemplateDefinition>(new[]
                {
                    new EntityTemplateDefinition { key = "hero", displayName = "Hero" }
                })
            );

            ContentCatalogBuildResult result = builder.Build();

            Assert.That(result.ValidationResult.Messages.Count(message => message.Code == "content.missing_reference"), Is.EqualTo(2));
        }

        private sealed class StaticContentLoader<TDefinition> : IContentLoader<TDefinition>
        {
            private readonly IReadOnlyList<TDefinition> definitions;

            public StaticContentLoader(IReadOnlyList<TDefinition> definitions)
            {
                this.definitions = definitions;
            }

            public IReadOnlyList<TDefinition> Load() => definitions;

            public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<TDefinition> definitions) =>
                Array.Empty<ValidationMessage>();
        }
    }
}
