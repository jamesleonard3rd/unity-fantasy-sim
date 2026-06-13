using System.Text;
using FantasySim.Persistence.SQLite;
using UnityEditor;
using UnityEngine;

namespace FantasySim.Editor.DatabaseTools
{
    public static class DatabaseMenuItems
    {
        [MenuItem("Fantasy Sim/Database/Rebuild Development Database")]
        public static void RebuildDevelopmentDatabase()
        {
            DatabaseInitializer initializer = CreateDevelopmentInitializer();
            initializer.Rebuild();

            Debug.Log($"Rebuilt Fantasy Sim development database at: {initializer.DatabasePath}");
        }

        [MenuItem("Fantasy Sim/Database/Log Database Status")]
        public static void LogDatabaseStatus()
        {
            DatabaseInitializer initializer = CreateDevelopmentInitializer();
            initializer.Initialize();

            DatabaseStatus status = initializer.GetStatus();
            var builder = new StringBuilder();
            builder.AppendLine($"Fantasy Sim database: {status.DatabasePath}");
            builder.AppendLine($"Applied migrations: {status.AppliedMigrations.Count}");

            foreach (string migrationId in status.AppliedMigrations)
            {
                builder.AppendLine($"- {migrationId}");
            }

            builder.AppendLine("Table counts:");
            foreach (var tableCount in status.TableCounts)
            {
                builder.AppendLine($"- {tableCount.Key}: {tableCount.Value}");
            }

            Debug.Log(builder.ToString());
        }

        private static DatabaseInitializer CreateDevelopmentInitializer()
        {
            return new DatabaseInitializer(DatabasePathProvider.ForEditorDevelopmentDatabase());
        }
    }
}
