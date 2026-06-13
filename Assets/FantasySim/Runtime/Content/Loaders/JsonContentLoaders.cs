using System;
using System.Collections.Generic;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using FantasySim.Core.Results;

namespace FantasySim.Content.Loaders
{
    public sealed class FactionJsonLoader : IContentLoader<FactionDefinition>
    {
        private readonly string path;

        public FactionJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<FactionDefinition> Load()
        {
            FactionFile file = JsonContentLoaderUtility.LoadFile<FactionFile>(path);
            FactionJson[] factions = file?.factions ?? Array.Empty<FactionJson>();
            var definitions = new FactionDefinition[factions.Length];

            for (int index = 0; index < factions.Length; index++)
            {
                FactionJson faction = factions[index];
                definitions[index] = new FactionDefinition
                {
                    key = ContentKeyNormalizer.Normalize(LoaderMapping.FirstNonEmpty(faction.key, faction.name)),
                    displayName = faction.name,
                    type = faction.type,
                    parentKey = ContentKeyNormalizer.Normalize(faction.parent),
                    description = faction.description
                };
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<FactionDefinition> definitions) =>
            LoaderValidation.ValidateDefinitions(definitions, definition => definition.key, definition => definition.displayName, "factions");

        [Serializable]
        private sealed class FactionFile
        {
            public FactionJson[] factions;
        }

        [Serializable]
        private sealed class FactionJson
        {
            public string key;
            public string name;
            public string type;
            public string parent;
            public string description;
        }
    }

    public sealed class RaceJsonLoader : IContentLoader<RaceDefinition>
    {
        private readonly string path;

        public RaceJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<RaceDefinition> Load()
        {
            RaceFile file = JsonContentLoaderUtility.LoadFile<RaceFile>(path);
            RaceJson[] races = file?.races ?? Array.Empty<RaceJson>();
            var definitions = new RaceDefinition[races.Length];

            for (int index = 0; index < races.Length; index++)
            {
                RaceJson race = races[index];
                definitions[index] = new RaceDefinition
                {
                    key = ContentKeyNormalizer.Normalize(LoaderMapping.FirstNonEmpty(race.key, race.name)),
                    displayName = race.name,
                    description = race.description,
                    subraces = MapSubraces(race.subraces)
                };
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<RaceDefinition> definitions) =>
            LoaderValidation.ValidateDefinitions(definitions, definition => definition.key, definition => definition.displayName, "races");

        private static SubraceDefinition[] MapSubraces(SubraceJson[] subraces)
        {
            if (subraces == null)
            {
                return Array.Empty<SubraceDefinition>();
            }

            var definitions = new SubraceDefinition[subraces.Length];
            for (int index = 0; index < subraces.Length; index++)
            {
                SubraceJson subrace = subraces[index];
                definitions[index] = new SubraceDefinition
                {
                    key = ContentKeyNormalizer.Normalize(LoaderMapping.FirstNonEmpty(subrace.key, subrace.name)),
                    displayName = subrace.name,
                    description = subrace.description
                };
            }

            return definitions;
        }

        [Serializable]
        private sealed class RaceFile
        {
            public RaceJson[] races;
        }

        [Serializable]
        private sealed class RaceJson
        {
            public string key;
            public string name;
            public string description;
            public SubraceJson[] subraces;
        }

        [Serializable]
        private sealed class SubraceJson
        {
            public string key;
            public string name;
            public string description;
        }
    }

    public sealed class StatJsonLoader : IContentLoader<StatDefinition>
    {
        private readonly string path;

        public StatJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<StatDefinition> Load()
        {
            StatFile file = JsonContentLoaderUtility.LoadFile<StatFile>(path);
            StatJson[] stats = file?.stats ?? Array.Empty<StatJson>();
            var definitions = new StatDefinition[stats.Length];

            for (int index = 0; index < stats.Length; index++)
            {
                StatJson stat = stats[index];
                definitions[index] = new StatDefinition
                {
                    key = ContentKeyNormalizer.Normalize(LoaderMapping.FirstNonEmpty(stat.key, stat.name)),
                    displayName = stat.name,
                    description = stat.description
                };
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<StatDefinition> definitions) =>
            LoaderValidation.ValidateDefinitions(definitions, definition => definition.key, definition => definition.displayName, "stats");

        [Serializable]
        private sealed class StatFile
        {
            public StatJson[] stats;
        }

        [Serializable]
        private sealed class StatJson
        {
            public string key;
            public string name;
            public string description;
        }
    }

    public sealed class TraitJsonLoader : IContentLoader<TraitDefinition>
    {
        private readonly string path;

        public TraitJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<TraitDefinition> Load()
        {
            TraitFile file = JsonContentLoaderUtility.LoadFile<TraitFile>(path);
            TraitJson[] traits = file?.traits ?? Array.Empty<TraitJson>();
            var definitions = new TraitDefinition[traits.Length];

            for (int index = 0; index < traits.Length; index++)
            {
                TraitJson trait = traits[index];
                definitions[index] = new TraitDefinition
                {
                    key = ContentKeyNormalizer.Normalize(LoaderMapping.FirstNonEmpty(trait.key, trait.name)),
                    displayName = trait.name,
                    description = trait.description,
                    modifiers = MapModifiers(trait.modifiers),
                    requiredTraitKeys = LoaderMapping.NormalizeArray(trait.requires),
                    evolvesToKey = ContentKeyNormalizer.Normalize(trait.evolves_to)
                };
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<TraitDefinition> definitions) =>
            LoaderValidation.ValidateDefinitions(definitions, definition => definition.key, definition => definition.displayName, "traits");

        private static TraitModifierDefinition[] MapModifiers(TraitModifierJson[] modifiers)
        {
            if (modifiers == null)
            {
                return Array.Empty<TraitModifierDefinition>();
            }

            var definitions = new List<TraitModifierDefinition>(modifiers.Length);
            foreach (TraitModifierJson modifier in modifiers)
            {
                if (string.IsNullOrWhiteSpace(modifier.stat)
                    && string.IsNullOrWhiteSpace(modifier.type)
                    && modifier.value == 0)
                {
                    continue;
                }

                definitions.Add(new TraitModifierDefinition
                {
                    statKey = ContentKeyNormalizer.Normalize(modifier.stat),
                    type = modifier.type,
                    value = modifier.value
                });
            }

            return definitions.ToArray();
        }

        [Serializable]
        private sealed class TraitFile
        {
            public TraitJson[] traits;
        }

        [Serializable]
        private sealed class TraitJson
        {
            public string key;
            public string name;
            public string description;
            public TraitModifierJson[] modifiers;
            public string[] requires;
            public string evolves_to;
        }

        [Serializable]
        private sealed class TraitModifierJson
        {
            public string stat;
            public string type;
            public int value;
        }
    }

    public sealed class RegionTemplateJsonLoader : IContentLoader<RegionDefinition>
    {
        public const float DefaultTickIntervalSeconds = 180f;

        private readonly string path;

        public RegionTemplateJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<RegionDefinition> Load()
        {
            RegionFile file = JsonContentLoaderUtility.LoadFile<RegionFile>(path);
            RegionJson[] regions = file?.regions ?? Array.Empty<RegionJson>();
            var definitions = new RegionDefinition[regions.Length];

            for (int index = 0; index < regions.Length; index++)
            {
                RegionJson region = regions[index];
                definitions[index] = new RegionDefinition
                {
                    key = ContentKeyNormalizer.Normalize(LoaderMapping.FirstNonEmpty(region.key, region.name)),
                    displayName = region.name,
                    type = region.type,
                    parentKey = ContentKeyNormalizer.Normalize(region.parent),
                    tickIntervalSeconds = region.tick_interval_seconds > 0f
                        ? region.tick_interval_seconds
                        : DefaultTickIntervalSeconds
                };
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<RegionDefinition> definitions) =>
            LoaderValidation.ValidateDefinitions(definitions, definition => definition.key, definition => definition.displayName, "regions");

        [Serializable]
        private sealed class RegionFile
        {
            public RegionJson[] regions;
        }

        [Serializable]
        private sealed class RegionJson
        {
            public string key;
            public string name;
            public string type;
            public string parent;
            public float tick_interval_seconds;
        }
    }

    public sealed class EntityTemplateJsonLoader : IContentLoader<EntityTemplateDefinition>
    {
        private readonly string path;

        public EntityTemplateJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<EntityTemplateDefinition> Load()
        {
            EntityTemplateFile file = JsonContentLoaderUtility.LoadFile<EntityTemplateFile>(path);
            EntityTemplateJson[] entities = file?.entities ?? Array.Empty<EntityTemplateJson>();
            var definitions = new EntityTemplateDefinition[entities.Length];

            for (int index = 0; index < entities.Length; index++)
            {
                EntityTemplateJson entity = entities[index];
                definitions[index] = new EntityTemplateDefinition
                {
                    key = ContentKeyNormalizer.Normalize(LoaderMapping.FirstNonEmpty(entity.id, entity.key, entity.name)),
                    displayName = entity.name,
                    raceKey = ContentKeyNormalizer.Normalize(entity.race),
                    subraceKey = ContentKeyNormalizer.Normalize(entity.subrace),
                    gender = entity.gender,
                    houseKey = ContentKeyNormalizer.Normalize(entity.house),
                    role = entity.role,
                    regionKey = ContentKeyNormalizer.Normalize(entity.region),
                    age = entity.age,
                    traitKeys = LoaderMapping.NormalizeArray(entity.traits),
                    magicKeys = LoaderMapping.NormalizeArray(entity.magic),
                    factions = MapFactionMemberships(entity.factions),
                    notes = entity.notes
                };
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<EntityTemplateDefinition> definitions) =>
            LoaderValidation.ValidateDefinitions(definitions, definition => definition.key, definition => definition.displayName, "entity templates");

        private static EntityFactionMembershipDefinition[] MapFactionMemberships(EntityFactionMembershipJson[] factions)
        {
            if (factions == null)
            {
                return Array.Empty<EntityFactionMembershipDefinition>();
            }

            var definitions = new EntityFactionMembershipDefinition[factions.Length];
            for (int index = 0; index < factions.Length; index++)
            {
                EntityFactionMembershipJson faction = factions[index];
                definitions[index] = new EntityFactionMembershipDefinition
                {
                    factionKey = ContentKeyNormalizer.Normalize(faction.name),
                    rank = faction.rank
                };
            }

            return definitions;
        }

        [Serializable]
        private sealed class EntityTemplateFile
        {
            public EntityTemplateJson[] entities;
        }

        [Serializable]
        private sealed class EntityTemplateJson
        {
            public string id;
            public string key;
            public string name;
            public string race;
            public string subrace;
            public string gender;
            public string house;
            public string role;
            public string region;
            public int age;
            public string[] traits;
            public string[] magic;
            public EntityFactionMembershipJson[] factions;
            public string notes;
        }

        [Serializable]
        private sealed class EntityFactionMembershipJson
        {
            public string name;
            public string rank;
        }
    }

    internal static class LoaderValidation
    {
        public static IReadOnlyList<ValidationMessage> ValidateDefinitions<TDefinition>(
            IReadOnlyList<TDefinition> definitions,
            Func<TDefinition, string> keySelector,
            Func<TDefinition, string> displayNameSelector,
            string contentGroupName
        )
        {
            var messages = new List<ValidationMessage>();
            messages.AddRange(ContentKeyValidator.ValidateUniqueKeys(definitions, keySelector, contentGroupName).Messages);
            messages.AddRange(ContentKeyValidator.ValidateRequiredDisplayNames(definitions, displayNameSelector, contentGroupName).Messages);
            messages.AddRange(ContentKeyValidator.ValidateUnambiguousNormalizedDisplayNames(definitions, displayNameSelector, contentGroupName).Messages);
            return messages;
        }
    }

    internal static class LoaderMapping
    {
        public static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        public static string[] NormalizeArray(string[] values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var normalizedValues = new List<string>(values.Length);
            foreach (string value in values)
            {
                string normalizedValue = ContentKeyNormalizer.Normalize(value);
                if (!string.IsNullOrWhiteSpace(normalizedValue))
                {
                    normalizedValues.Add(normalizedValue);
                }
            }

            return normalizedValues.ToArray();
        }
    }
}
