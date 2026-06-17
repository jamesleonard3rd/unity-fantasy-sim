using System.Collections.Generic;
using FantasySim.Content.Definitions;

namespace FantasySim.Content.Catalog
{
    public sealed class ContentCatalog
    {
        public ContentCatalog(
            IReadOnlyDictionary<string, FactionDefinition> factions,
            IReadOnlyDictionary<string, RaceDefinition> races,
            IReadOnlyDictionary<string, StatDefinition> stats,
            IReadOnlyDictionary<string, TraitDefinition> traits,
            IReadOnlyDictionary<string, RegionDefinition> regions,
            IReadOnlyDictionary<string, EntityTemplateDefinition> entityTemplates,
            IReadOnlyDictionary<string, GoalDefinition> goals = null,
            IReadOnlyList<EntityRelationshipDefinition> entityRelationships = null
        )
        {
            Factions = factions;
            Races = races;
            Stats = stats;
            Traits = traits;
            Regions = regions;
            EntityTemplates = entityTemplates;
            Goals = goals ?? new Dictionary<string, GoalDefinition>();
            EntityRelationships = entityRelationships ?? new List<EntityRelationshipDefinition>();
        }

        public static ContentCatalog Empty { get; } = new ContentCatalog(
            new Dictionary<string, FactionDefinition>(),
            new Dictionary<string, RaceDefinition>(),
            new Dictionary<string, StatDefinition>(),
            new Dictionary<string, TraitDefinition>(),
            new Dictionary<string, RegionDefinition>(),
            new Dictionary<string, EntityTemplateDefinition>()
        );

        public IReadOnlyDictionary<string, FactionDefinition> Factions { get; }
        public IReadOnlyDictionary<string, RaceDefinition> Races { get; }
        public IReadOnlyDictionary<string, StatDefinition> Stats { get; }
        public IReadOnlyDictionary<string, TraitDefinition> Traits { get; }
        public IReadOnlyDictionary<string, RegionDefinition> Regions { get; }
        public IReadOnlyDictionary<string, EntityTemplateDefinition> EntityTemplates { get; }
        public IReadOnlyDictionary<string, GoalDefinition> Goals { get; }
        public IReadOnlyList<EntityRelationshipDefinition> EntityRelationships { get; }
    }
}
