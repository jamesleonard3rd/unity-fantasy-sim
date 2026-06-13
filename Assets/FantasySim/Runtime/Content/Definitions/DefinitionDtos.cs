using System;

namespace FantasySim.Content.Definitions
{
    [Serializable]
    public sealed class FactionDefinition
    {
        public string key;
        public string displayName;
        public string kind;
        public string parentKey;
        public string description;
    }

    [Serializable]
    public sealed class RaceDefinition
    {
        public string key;
        public string displayName;
        public string description;
    }

    [Serializable]
    public sealed class StatDefinition
    {
        public string key;
        public string displayName;
        public string description;
    }

    [Serializable]
    public sealed class TraitDefinition
    {
        public string key;
        public string displayName;
        public string description;
        public string rarity;
    }

    [Serializable]
    public sealed class RegionDefinition
    {
        public string key;
        public string displayName;
        public string type;
        public string parentKey;
        public float tickIntervalSeconds;
    }

    [Serializable]
    public sealed class EntityTemplateDefinition
    {
        public string key;
        public string displayName;
        public string raceKey;
        public string subraceKey;
    }
}
