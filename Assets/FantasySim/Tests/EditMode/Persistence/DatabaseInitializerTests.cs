using System;
using System.IO;
using FantasySim.Persistence.SQLite;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Persistence
{
    public sealed class DatabaseInitializerTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimTests_{Guid.NewGuid():N}");
            databasePath = Path.Combine(tempDirectory, DatabasePathProvider.DatabaseFileName);
            pathProvider = new DatabasePathProvider(databasePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void InitializeCreatesRequiredTables()
        {
            var initializer = new DatabaseInitializer(pathProvider);

            initializer.Initialize();

            Assert.That(File.Exists(databasePath), Is.True);

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            AssertTableExists(connection, "schema_migrations");
            AssertTableExists(connection, "world_clock");
            AssertTableExists(connection, "regions");
            AssertTableExists(connection, "simulation_partitions");
            AssertTableExists(connection, "simulation_partition_regions");
            AssertTableExists(connection, "entities");
            AssertTableExists(connection, "entity_positions");
            AssertTableExists(connection, "relationships");
            AssertTableExists(connection, "relationship_terms");
            AssertTableExists(connection, "world_events");
        }

        [Test]
        public void InitializeIsIdempotent()
        {
            var initializer = new DatabaseInitializer(pathProvider);

            var firstRun = initializer.Initialize();
            var secondRun = initializer.Initialize();
            DatabaseStatus status = initializer.GetStatus();

            Assert.That(firstRun, Is.EquivalentTo(new[] { "001_initial_save_state" }));
            Assert.That(secondRun, Is.Empty);
            Assert.That(status.AppliedMigrations, Is.EquivalentTo(new[] { "001_initial_save_state" }));
        }

        [Test]
        public void ConnectionFactoryEnablesForeignKeys()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.Initialize();

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();

            Assert.That(connection.ExecuteScalar<int>("PRAGMA foreign_keys"), Is.EqualTo(1));
        }

        private static void AssertTableExists(SQLiteConnection connection, string tableName)
        {
            int count = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?",
                tableName
            );

            Assert.That(count, Is.EqualTo(1), $"Expected table '{tableName}' to exist.");
        }
    }
}
