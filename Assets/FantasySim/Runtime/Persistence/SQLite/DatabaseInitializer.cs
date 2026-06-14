using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasySim.Content.Catalog;
using FantasySim.Persistence.Migrations;
using SQLite;

namespace FantasySim.Persistence.SQLite
{
    public sealed class DatabaseInitializer
    {
        private static readonly string[] StatusTables =
        {
            MigrationRunner.SchemaMigrationsTable,
            "world_clock",
            "regions",
            "simulation_partitions",
            "simulation_partition_regions",
            "entities",
            "entity_positions",
            "entity_needs",
            "relationships",
            "relationship_terms",
            "world_events"
        };

        private readonly DatabasePathProvider pathProvider;
        private readonly SqliteConnectionFactory connectionFactory;
        private readonly MigrationRunner migrationRunner;
        private readonly StarterWorldGenerator starterWorldGenerator;

        public DatabaseInitializer(DatabasePathProvider pathProvider = null)
        {
            this.pathProvider = pathProvider ?? DatabasePathProvider.ForDefaultGameDatabase();
            connectionFactory = new SqliteConnectionFactory(this.pathProvider);
            migrationRunner = new MigrationRunner();
            starterWorldGenerator = new StarterWorldGenerator();
        }

        public string DatabasePath => pathProvider.DatabasePath;

        public IReadOnlyList<string> Initialize()
        {
            using SQLiteConnection connection = connectionFactory.OpenConnection();
            return migrationRunner.ApplyMigrations(connection, InitialMigrations.All);
        }

        public void Rebuild()
        {
            DeleteDatabaseFiles();
            Initialize();
        }

        public StarterWorldGenerationResult GenerateStarterWorldIfEmpty(ContentCatalogBuildResult catalogBuildResult = null)
        {
            Initialize();

            ContentCatalogBuildResult result = catalogBuildResult ?? new ContentCatalogBuilder().Build();
            if (!result.IsValid)
            {
                string validationMessages = string.Join(
                    "\n",
                    result.ValidationResult.Messages.Select(ContentCatalogValidationReport.FormatMessage)
                );
                throw new System.InvalidOperationException(
                    $"Cannot generate starter world from invalid game data:\n{validationMessages}"
                );
            }

            using SQLiteConnection connection = connectionFactory.OpenConnection();
            return starterWorldGenerator.GenerateIfEmpty(connection, result.Catalog);
        }

        public DatabaseStatus GetStatus()
        {
            using SQLiteConnection connection = connectionFactory.OpenConnection();
            IReadOnlyList<string> appliedMigrations = migrationRunner.GetAppliedMigrationIds(connection);
            var tableCounts = new Dictionary<string, int>();

            foreach (string table in StatusTables)
            {
                if (TableExists(connection, table))
                {
                    tableCounts[table] = connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {table}");
                }
            }

            return new DatabaseStatus(DatabasePath, appliedMigrations, tableCounts);
        }

        private void DeleteDatabaseFiles()
        {
            DeleteIfExists(DatabasePath);
            DeleteIfExists($"{DatabasePath}-wal");
            DeleteIfExists($"{DatabasePath}-shm");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool TableExists(SQLiteConnection connection, string tableName)
        {
            return connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?",
                tableName
            ) > 0;
        }
    }
}
