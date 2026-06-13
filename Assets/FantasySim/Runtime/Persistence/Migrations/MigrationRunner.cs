using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

namespace FantasySim.Persistence.Migrations
{
    public sealed class MigrationRunner
    {
        public const string SchemaMigrationsTable = "schema_migrations";

        public IReadOnlyList<string> ApplyMigrations(
            SQLiteConnection connection,
            IEnumerable<SqlMigration> migrations
        )
        {
            EnsureSchemaMigrationsTable(connection);

            var appliedMigrationIds = new HashSet<string>(
                connection.Query<SchemaMigrationRow>(
                    $"SELECT id FROM {SchemaMigrationsTable} ORDER BY id"
                ).Select(row => row.Id)
            );

            var appliedThisRun = new List<string>();
            foreach (SqlMigration migration in migrations.OrderBy(migration => migration.Id))
            {
                if (appliedMigrationIds.Contains(migration.Id))
                {
                    continue;
                }

                ApplyMigration(connection, migration);
                appliedThisRun.Add(migration.Id);
                appliedMigrationIds.Add(migration.Id);
            }

            return appliedThisRun;
        }

        public IReadOnlyList<string> GetAppliedMigrationIds(SQLiteConnection connection)
        {
            EnsureSchemaMigrationsTable(connection);

            return connection.Query<SchemaMigrationRow>(
                $"SELECT id FROM {SchemaMigrationsTable} ORDER BY id"
            ).Select(row => row.Id).ToArray();
        }

        private static void EnsureSchemaMigrationsTable(SQLiteConnection connection)
        {
            connection.Execute(
                $@"CREATE TABLE IF NOT EXISTS {SchemaMigrationsTable} (
                    id TEXT PRIMARY KEY,
                    description TEXT NOT NULL,
                    applied_utc TEXT NOT NULL
                )"
            );
        }

        private static void ApplyMigration(SQLiteConnection connection, SqlMigration migration)
        {
            connection.RunInTransaction(() =>
            {
                foreach (string statement in migration.Statements)
                {
                    connection.Execute(statement);
                }

                connection.Execute(
                    $@"INSERT INTO {SchemaMigrationsTable} (id, description, applied_utc)
                       VALUES (?, ?, ?)",
                    migration.Id,
                    migration.Description,
                    DateTime.UtcNow.ToString("O")
                );
            });
        }

        private sealed class SchemaMigrationRow
        {
            [Column("id")]
            public string Id { get; set; }
        }
    }
}
