using System;
using System.Collections.Generic;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;

namespace FantasySim.Tests.EditMode.Session
{
    internal static class LiveRegionTestWorld
    {
        public const string ParentRegionKey = "parent_region";
        public const string ChildRegionKey = "child_region";

        public static ContentCatalogBuildResult CreateValidBuildResult()
        {
            var regions = new Dictionary<string, RegionDefinition>
            {
                {
                    ParentRegionKey,
                    new RegionDefinition
                    {
                        key = ParentRegionKey,
                        displayName = "Parent Region",
                        type = "region",
                        tickIntervalSeconds = 180f
                    }
                },
                {
                    ChildRegionKey,
                    new RegionDefinition
                    {
                        key = ChildRegionKey,
                        displayName = "Child Region",
                        type = "castle",
                        parentKey = ParentRegionKey,
                        tickIntervalSeconds = 120f
                    }
                }
            };

            var entityTemplates = new Dictionary<string, EntityTemplateDefinition>
            {
                {
                    "hero",
                    new EntityTemplateDefinition
                    {
                        key = "hero",
                        displayName = "Hero",
                        raceKey = "human",
                        subraceKey = "standard_human",
                        gender = "F",
                        houseKey = "house_valemont",
                        role = "heir",
                        regionKey = ChildRegionKey,
                        age = 30,
                        traitKeys = new[] { "strong" },
                        magicKeys = Array.Empty<string>(),
                        factions = Array.Empty<EntityFactionMembershipDefinition>(),
                        notes = "Test entity."
                    }
                }
            };

            var catalog = new ContentCatalog(
                new Dictionary<string, FactionDefinition>(),
                new Dictionary<string, RaceDefinition>(),
                new Dictionary<string, StatDefinition>(),
                new Dictionary<string, TraitDefinition>(),
                regions,
                entityTemplates
            );

            return new ContentCatalogBuildResult(catalog, ContentValidationResult.Success);
        }
    }
}
