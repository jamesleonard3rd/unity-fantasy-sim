using System;

namespace FantasySim.Content.Definitions
{
    [Serializable]
    public sealed class FactionDefinition
    {
        public string key;
        public string displayName;
        public string type;
        public string parentKey;
        public string description;
    }

    [Serializable]
    public sealed class RaceDefinition
    {
        public string key;
        public string displayName;
        public string description;
        public SubraceDefinition[] subraces;
    }

    [Serializable]
    public sealed class SubraceDefinition
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
        public TraitModifierDefinition[] modifiers;
        public string[] requiredTraitKeys;
        public string evolvesToKey;
    }

    [Serializable]
    public sealed class TraitModifierDefinition
    {
        public string statKey;
        public string type;
        public int value;
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
        public string gender;
        public string houseKey;
        public string role;
        public string regionKey;
        public int age;
        public string[] traitKeys;
        public string[] magicKeys;
        public EntityFactionMembershipDefinition[] factions;
        public string notes;
    }

    [Serializable]
    public sealed class EntityFactionMembershipDefinition
    {
        public string factionKey;
        public string rank;
    }

    [Serializable]
    public sealed class EntityRelationshipDefinition
    {
        public string fromKey;
        public string toKey;
        public string type;
        public int opinion;
    }

    [Serializable]
    public sealed class GoalDefinition
    {
        public string key;
        public string displayName;
        public string completionMode;
        public string[] requires;
        public string rawJson;
    }
}
