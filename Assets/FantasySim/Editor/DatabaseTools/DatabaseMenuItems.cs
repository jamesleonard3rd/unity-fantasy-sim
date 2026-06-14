using System.Text;
using FantasySim.Content.Catalog;
using FantasySim.Core.Results;
using FantasySim.Persistence.SQLite;
using UnityEditor;
using UnityEngine;

namespace FantasySim.Editor.DatabaseTools
{
    public static class DatabaseMenuItems
    {
        [MenuItem("Fantasy Sim/Validate Game Data")]
        public static void ValidateGameData()
        {
            ContentCatalogBuildResult result = new ContentCatalogBuilder().Build();
            LogValidationResult(result);
        }

        [MenuItem("Fantasy Sim/Database/Rebuild Development Database")]
        public static void RebuildDevelopmentDatabase()
        {
            DatabaseInitializer initializer = CreateDevelopmentInitializer();
            initializer.Rebuild();

            Debug.Log($"Rebuilt Fantasy Sim development database at: {initializer.DatabasePath}");
        }

        [MenuItem("Fantasy Sim/Database/Generate Starter World")]
        public static void GenerateStarterWorld()
        {
            DatabaseInitializer initializer = CreateDevelopmentInitializer();
            StarterWorldGenerationResult result = initializer.GenerateStarterWorldIfEmpty();

            Debug.Log(
                $"{result.Message}\n" +
                $"Database: {initializer.DatabasePath}\n" +
                $"Regions inserted: {result.RegionsInserted}\n" +
                $"Partitions inserted: {result.PartitionsInserted}\n" +
                $"Entities inserted: {result.EntitiesInserted}\n" +
                $"Needs inserted: {result.NeedsInserted}"
            );
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

        private static void LogValidationResult(ContentCatalogBuildResult result)
        {
            if (result.IsValid)
            {
                Debug.Log(ContentCatalogValidationReport.FormatSummary(result));
            }
            else
            {
                Debug.LogError(ContentCatalogValidationReport.FormatSummary(result));
            }

            foreach (ValidationMessage message in result.ValidationResult.Messages)
            {
                string formattedMessage = ContentCatalogValidationReport.FormatMessage(message);
                switch (message.Severity)
                {
                    case ValidationSeverity.Error:
                        Debug.LogError(formattedMessage);
                        break;
                    case ValidationSeverity.Warning:
                        Debug.LogWarning(formattedMessage);
                        break;
                    default:
                        Debug.Log(formattedMessage);
                        break;
                }
            }
        }

        private static DatabaseInitializer CreateDevelopmentInitializer()
        {
            return new DatabaseInitializer(DatabasePathProvider.ForEditorDevelopmentDatabase());
        }
    }
}
