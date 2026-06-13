using System;
using System.IO;
using UnityEngine;

namespace FantasySim.Persistence.SQLite
{
    public sealed class DatabasePathProvider
    {
        public const string DatabaseFileName = "game.db";

        private readonly string pathOverride;

        public DatabasePathProvider(string pathOverride = null)
        {
            this.pathOverride = pathOverride;
        }

        public string DatabasePath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(pathOverride))
                {
                    return pathOverride;
                }

                return Path.Combine(Application.persistentDataPath, DatabaseFileName);
            }
        }

        public static DatabasePathProvider ForDefaultGameDatabase()
        {
            return new DatabasePathProvider();
        }

        public static DatabasePathProvider ForEditorDevelopmentDatabase()
        {
            return new DatabasePathProvider(
                Path.Combine(Application.persistentDataPath, "Development", DatabaseFileName)
            );
        }

        public void EnsureDirectoryExists()
        {
            string directory = Path.GetDirectoryName(DatabasePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException($"Database path has no directory: {DatabasePath}");
            }

            Directory.CreateDirectory(directory);
        }
    }
}
