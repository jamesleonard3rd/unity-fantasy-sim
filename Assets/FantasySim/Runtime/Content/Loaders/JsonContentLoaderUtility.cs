using System.IO;
using UnityEngine;

namespace FantasySim.Content.Loaders
{
    internal static class JsonContentLoaderUtility
    {
        public static T LoadFile<T>(string path)
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }
    }
}
