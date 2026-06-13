using System.Collections.Generic;
using FantasySim.Content.Definitions;
using FantasySim.Content.Loaders;
using FantasySim.Content.Validation;
using FantasySim.Core.Results;

namespace FantasySim.Content.Catalog
{
    public sealed class ContentCatalogBuilder
    {
        private readonly IContentLoader<FactionDefinition> factionLoader;
        private readonly IContentLoader<RaceDefinition> raceLoader;
        private readonly IContentLoader<StatDefinition> statLoader;
        private readonly IContentLoader<TraitDefinition> traitLoader;
        private readonly IContentLoader<RegionDefinition> regionLoader;
        private readonly IContentLoader<EntityTemplateDefinition> entityTemplateLoader;

        public ContentCatalogBuilder(string gameDataRoot = null)
            : this(
                new FactionJsonLoader(GameDataJsonPaths.Factions(gameDataRoot ?? GameDataJsonPaths.DefaultRoot)),
                new RaceJsonLoader(GameDataJsonPaths.Races(gameDataRoot ?? GameDataJsonPaths.DefaultRoot)),
                new StatJsonLoader(GameDataJsonPaths.Stats(gameDataRoot ?? GameDataJsonPaths.DefaultRoot)),
                new TraitJsonLoader(GameDataJsonPaths.Traits(gameDataRoot ?? GameDataJsonPaths.DefaultRoot)),
                new RegionTemplateJsonLoader(GameDataJsonPaths.Regions(gameDataRoot ?? GameDataJsonPaths.DefaultRoot)),
                new EntityTemplateJsonLoader(GameDataJsonPaths.EntityTemplates(gameDataRoot ?? GameDataJsonPaths.DefaultRoot))
            )
        {
        }

        public ContentCatalogBuilder(
            IContentLoader<FactionDefinition> factionLoader,
            IContentLoader<RaceDefinition> raceLoader,
            IContentLoader<StatDefinition> statLoader,
            IContentLoader<TraitDefinition> traitLoader,
            IContentLoader<RegionDefinition> regionLoader,
            IContentLoader<EntityTemplateDefinition> entityTemplateLoader
        )
        {
            this.factionLoader = factionLoader;
            this.raceLoader = raceLoader;
            this.statLoader = statLoader;
            this.traitLoader = traitLoader;
            this.regionLoader = regionLoader;
            this.entityTemplateLoader = entityTemplateLoader;
        }

        public ContentCatalogBuildResult Build()
        {
            IReadOnlyList<FactionDefinition> factions = factionLoader.Load();
            IReadOnlyList<RaceDefinition> races = raceLoader.Load();
            IReadOnlyList<StatDefinition> stats = statLoader.Load();
            IReadOnlyList<TraitDefinition> traits = traitLoader.Load();
            IReadOnlyList<RegionDefinition> regions = regionLoader.Load();
            IReadOnlyList<EntityTemplateDefinition> entityTemplates = entityTemplateLoader.Load();

            var messages = new List<ValidationMessage>();
            messages.AddRange(factionLoader.Validate(factions));
            messages.AddRange(raceLoader.Validate(races));
            messages.AddRange(statLoader.Validate(stats));
            messages.AddRange(traitLoader.Validate(traits));
            messages.AddRange(regionLoader.Validate(regions));
            messages.AddRange(entityTemplateLoader.Validate(entityTemplates));

            Dictionary<string, FactionDefinition> factionsByKey = IndexByKey(factions, definition => definition.key);
            Dictionary<string, RaceDefinition> racesByKey = IndexByKey(races, definition => definition.key);
            Dictionary<string, StatDefinition> statsByKey = IndexByKey(stats, definition => definition.key);
            Dictionary<string, TraitDefinition> traitsByKey = IndexByKey(traits, definition => definition.key);
            Dictionary<string, RegionDefinition> regionsByKey = IndexByKey(regions, definition => definition.key);
            Dictionary<string, EntityTemplateDefinition> entityTemplatesByKey = IndexByKey(entityTemplates, definition => definition.key);

            ValidateFactionReferences(factions, factionsByKey, messages);
            ValidateRegionReferences(regions, regionsByKey, messages);
            ValidateTraitReferences(traits, statsByKey, traitsByKey, messages);
            ValidateEntityTemplateReferences(entityTemplates, racesByKey, factionsByKey, regionsByKey, traitsByKey, messages);

            var catalog = new ContentCatalog(
                factionsByKey,
                racesByKey,
                statsByKey,
                traitsByKey,
                regionsByKey,
                entityTemplatesByKey
            );

            return new ContentCatalogBuildResult(catalog, new ContentValidationResult(messages));
        }

        private static Dictionary<string, TDefinition> IndexByKey<TDefinition>(
            IEnumerable<TDefinition> definitions,
            System.Func<TDefinition, string> keySelector
        )
        {
            var definitionsByKey = new Dictionary<string, TDefinition>();
            foreach (TDefinition definition in definitions)
            {
                string key = keySelector(definition);
                if (!string.IsNullOrWhiteSpace(key) && !definitionsByKey.ContainsKey(key))
                {
                    definitionsByKey.Add(key, definition);
                }
            }

            return definitionsByKey;
        }

        private static void ValidateFactionReferences(
            IEnumerable<FactionDefinition> factions,
            IReadOnlyDictionary<string, FactionDefinition> factionsByKey,
            List<ValidationMessage> messages
        )
        {
            foreach (FactionDefinition faction in factions)
            {
                AddMissingReference(
                    messages,
                    "factions",
                    faction.key,
                    "parentKey",
                    faction.parentKey,
                    factionsByKey.ContainsKey
                );
            }
        }

        private static void ValidateRegionReferences(
            IEnumerable<RegionDefinition> regions,
            IReadOnlyDictionary<string, RegionDefinition> regionsByKey,
            List<ValidationMessage> messages
        )
        {
            foreach (RegionDefinition region in regions)
            {
                AddMissingReference(
                    messages,
                    "regions",
                    region.key,
                    "parentKey",
                    region.parentKey,
                    regionsByKey.ContainsKey
                );
            }
        }

        private static void ValidateTraitReferences(
            IEnumerable<TraitDefinition> traits,
            IReadOnlyDictionary<string, StatDefinition> statsByKey,
            IReadOnlyDictionary<string, TraitDefinition> traitsByKey,
            List<ValidationMessage> messages
        )
        {
            foreach (TraitDefinition trait in traits)
            {
                AddMissingReference(
                    messages,
                    "traits",
                    trait.key,
                    "evolvesToKey",
                    trait.evolvesToKey,
                    traitsByKey.ContainsKey
                );

                foreach (string requiredTraitKey in trait.requiredTraitKeys ?? System.Array.Empty<string>())
                {
                    AddMissingReference(
                        messages,
                        "traits",
                        trait.key,
                        "requiredTraitKeys",
                        requiredTraitKey,
                        traitsByKey.ContainsKey
                    );
                }

                foreach (TraitModifierDefinition modifier in trait.modifiers ?? System.Array.Empty<TraitModifierDefinition>())
                {
                    AddMissingReference(
                        messages,
                        "traits",
                        trait.key,
                        "modifiers.statKey",
                        modifier.statKey,
                        statsByKey.ContainsKey
                    );
                }
            }
        }

        private static void ValidateEntityTemplateReferences(
            IEnumerable<EntityTemplateDefinition> entityTemplates,
            IReadOnlyDictionary<string, RaceDefinition> racesByKey,
            IReadOnlyDictionary<string, FactionDefinition> factionsByKey,
            IReadOnlyDictionary<string, RegionDefinition> regionsByKey,
            IReadOnlyDictionary<string, TraitDefinition> traitsByKey,
            List<ValidationMessage> messages
        )
        {
            foreach (EntityTemplateDefinition entityTemplate in entityTemplates)
            {
                AddMissingRequiredReference(messages, "entity templates", entityTemplate.key, "raceKey", entityTemplate.raceKey);
                AddMissingReference(messages, "entity templates", entityTemplate.key, "raceKey", entityTemplate.raceKey, racesByKey.ContainsKey);
                AddMissingReference(messages, "entity templates", entityTemplate.key, "houseKey", entityTemplate.houseKey, factionsByKey.ContainsKey);
                AddMissingRequiredReference(messages, "entity templates", entityTemplate.key, "regionKey", entityTemplate.regionKey);
                AddMissingReference(messages, "entity templates", entityTemplate.key, "regionKey", entityTemplate.regionKey, regionsByKey.ContainsKey);

                foreach (string traitKey in entityTemplate.traitKeys ?? System.Array.Empty<string>())
                {
                    AddMissingReference(messages, "entity templates", entityTemplate.key, "traitKeys", traitKey, traitsByKey.ContainsKey);
                }

                foreach (EntityFactionMembershipDefinition faction in entityTemplate.factions ?? System.Array.Empty<EntityFactionMembershipDefinition>())
                {
                    AddMissingReference(
                        messages,
                        "entity templates",
                        entityTemplate.key,
                        "factions.factionKey",
                        faction.factionKey,
                        factionsByKey.ContainsKey
                    );
                }
            }
        }

        private static void AddMissingRequiredReference(
            List<ValidationMessage> messages,
            string contentGroupName,
            string ownerKey,
            string fieldName,
            string referenceKey
        )
        {
            if (string.IsNullOrWhiteSpace(referenceKey))
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "content.missing_reference",
                    $"{contentGroupName} '{ownerKey}' is missing required reference '{fieldName}'."
                ));
            }
        }

        private static void AddMissingReference(
            List<ValidationMessage> messages,
            string contentGroupName,
            string ownerKey,
            string fieldName,
            string referenceKey,
            System.Func<string, bool> referenceExists
        )
        {
            if (string.IsNullOrWhiteSpace(referenceKey))
            {
                return;
            }

            if (!referenceExists(referenceKey))
            {
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Error,
                    "content.invalid_reference",
                    $"{contentGroupName} '{ownerKey}' references missing {fieldName} '{referenceKey}'."
                ));
            }
        }
    }
}
