using System.IO;
using UnityEngine;

namespace FantasySim.Content.Loaders
{
    public static class GameDataJsonPaths
    {
        public static string DefaultRoot => Path.Combine(Application.dataPath, "GameData", "Json");

        public static string Factions(string root) => Path.Combine(root, "factions", "factions.json");
        public static string Races(string root) => Path.Combine(root, "races", "races.json");
        public static string Stats(string root) => Path.Combine(root, "stats", "stats.json");
        public static string Traits(string root) => Path.Combine(root, "traits", "traits.json");
        public static string Regions(string root) => Path.Combine(root, "regions", "templates.json");
        public static string EntityTemplates(string root) => Path.Combine(root, "entities", "templates.json");
    }
}
