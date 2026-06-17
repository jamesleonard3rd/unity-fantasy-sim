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

    public sealed class EntityRelationshipJsonLoader : IContentLoader<EntityRelationshipDefinition>
    {
        private readonly string path;

        public EntityRelationshipJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<EntityRelationshipDefinition> Load()
        {
            EntityRelationshipFile file = JsonContentLoaderUtility.LoadFile<EntityRelationshipFile>(path);
            EntityRelationshipJson[] relationships = file?.relationships ?? Array.Empty<EntityRelationshipJson>();
            var definitions = new List<EntityRelationshipDefinition>(relationships.Length);

            foreach (EntityRelationshipJson relationship in relationships)
            {
                string fromKey = ContentKeyNormalizer.Normalize(relationship.from);
                string toKey = ContentKeyNormalizer.Normalize(relationship.to);
                if (string.IsNullOrWhiteSpace(fromKey) && string.IsNullOrWhiteSpace(toKey))
                {
                    continue;
                }

                definitions.Add(new EntityRelationshipDefinition
                {
                    fromKey = fromKey,
                    toKey = toKey,
                    type = relationship.type,
                    opinion = relationship.opinion
                });
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<EntityRelationshipDefinition> definitions)
        {
            var messages = new List<ValidationMessage>();
            foreach (EntityRelationshipDefinition relationship in definitions)
            {
                if (string.IsNullOrWhiteSpace(relationship.fromKey))
                {
                    messages.Add(new ValidationMessage(
                        ValidationSeverity.Error,
                        "content.missing_reference",
                        "entity relationships are missing a required 'from' entity."
                    ));
                }

                if (string.IsNullOrWhiteSpace(relationship.toKey))
                {
                    messages.Add(new ValidationMessage(
                        ValidationSeverity.Error,
                        "content.missing_reference",
                        $"entity relationship '{relationship.fromKey}' is missing a required 'to' entity."
                    ));
                }
            }

            return messages;
        }

        [Serializable]
        private sealed class EntityRelationshipFile
        {
            public EntityRelationshipJson[] relationships;
        }

        [Serializable]
        private sealed class EntityRelationshipJson
        {
            public string from;
            public string to;
            public string type;
            public int opinion;
        }
    }

    public sealed class GoalTemplateJsonLoader : IContentLoader<GoalDefinition>
    {
        private readonly string path;

        public GoalTemplateJsonLoader(string path)
        {
            this.path = path;
        }

        public IReadOnlyList<GoalDefinition> Load()
        {
            string json = System.IO.File.ReadAllText(path);
            var definitions = new List<GoalDefinition>();

            foreach (string element in JsonArraySplitter.SplitArrayElements(json, "goal_templates"))
            {
                GoalTemplateJson template = UnityEngine.JsonUtility.FromJson<GoalTemplateJson>(element);
                if (template == null || string.IsNullOrWhiteSpace(template.goal_type))
                {
                    continue;
                }

                definitions.Add(new GoalDefinition
                {
                    key = ContentKeyNormalizer.Normalize(template.goal_type),
                    displayName = template.goal_type,
                    completionMode = template.completion_mode,
                    requires = CopyNonEmpty(template.requires),
                    rawJson = element
                });
            }

            return definitions;
        }

        public IReadOnlyList<ValidationMessage> Validate(IReadOnlyList<GoalDefinition> definitions) =>
            LoaderValidation.ValidateDefinitions(definitions, definition => definition.key, definition => definition.displayName, "goal templates");

        private static string[] CopyNonEmpty(string[] values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var copied = new List<string>(values.Length);
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    copied.Add(value);
                }
            }

            return copied.ToArray();
        }

        [Serializable]
        private sealed class GoalTemplateJson
        {
            public string goal_type;
            public string completion_mode;
            public string[] requires;
        }
    }

    internal static class JsonArraySplitter
    {
        public static IReadOnlyList<string> SplitArrayElements(string json, string arrayPropertyName)
        {
            var elements = new List<string>();
            if (string.IsNullOrEmpty(json))
            {
                return elements;
            }

            int arrayStart = FindArrayStart(json, arrayPropertyName);
            if (arrayStart < 0)
            {
                return elements;
            }

            int depth = 0;
            int elementStart = -1;
            bool inString = false;
            bool escaped = false;

            for (int index = arrayStart; index < json.Length; index++)
            {
                char character = json[index];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                switch (character)
                {
                    case '"':
                        inString = true;
                        break;
                    case '[':
                    case '{':
                        depth++;
                        if (depth == 2 && character == '{' && elementStart < 0)
                        {
                            elementStart = index;
                        }

                        break;
                    case '}':
                    case ']':
                        depth--;
                        if (depth == 1 && character == '}' && elementStart >= 0)
                        {
                            elements.Add(json.Substring(elementStart, index - elementStart + 1));
                            elementStart = -1;
                        }
                        else if (depth == 0)
                        {
                            return elements;
                        }

                        break;
                }
            }

            return elements;
        }

        private static int FindArrayStart(string json, string arrayPropertyName)
        {
            string token = "\"" + arrayPropertyName + "\"";
            int propertyIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (propertyIndex < 0)
            {
                return -1;
            }

            int bracketIndex = json.IndexOf('[', propertyIndex + token.Length);
            return bracketIndex;
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
