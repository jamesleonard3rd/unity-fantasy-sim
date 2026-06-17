using System;
using System.Collections.Generic;
using System.IO;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using FantasySim.Persistence.SQLite;
using FantasySim.Session;
using FantasySim.Simulation;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Session
{
    public sealed class RegionSimulationServiceTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimRegionSimServiceTests_{Guid.NewGuid():N}");
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
        public void TickPartitionAdvancesClockAndWritesEvents()
        {
            GenerateStarterWorld();
            long partitionId = LoadStarterPartitionId();
            RegionSimulationService service = CreateService();

            PartitionTickReport report = service.TickPartition(partitionId);

            Assert.That(report.PartitionId, Is.EqualTo(partitionId));
            Assert.That(report.PreviousTick.Value, Is.EqualTo(0));
            Assert.That(report.CurrentTick.Value, Is.EqualTo(1));
            Assert.That(report.EventsInserted, Is.GreaterThanOrEqualTo(1));
            Assert.That(report.RegionsProcessed, Is.GreaterThanOrEqualTo(1));

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            long clockTick = connection.ExecuteScalar<long>("SELECT game_tick FROM world_clock WHERE id = 1");
            long eventCount = connection.ExecuteScalar<long>("SELECT COUNT(*) FROM world_events");
            Assert.That(clockTick, Is.EqualTo(1));
            Assert.That(eventCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void TickPartitionTwiceAdvancesClockEachTick()
        {
            GenerateStarterWorld();
            long partitionId = LoadStarterPartitionId();
            RegionSimulationService service = CreateService();

            service.TickPartition(partitionId);
            PartitionTickReport secondReport = service.TickPartition(partitionId);

            Assert.That(secondReport.PreviousTick.Value, Is.EqualTo(1));
            Assert.That(secondReport.CurrentTick.Value, Is.EqualTo(2));
        }

        private RegionSimulationService CreateService()
        {
            return new RegionSimulationService(
                new StarterWorldReadRepository(pathProvider),
                new RegionStateWriteRepository(pathProvider),
                new StarterWorldSimulationPipeline()
            );
        }

        private long LoadStarterPartitionId()
        {
            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            return connection.ExecuteScalar<long>(
                "SELECT id FROM simulation_partitions WHERE key = ?",
                StarterWorldReadRepository.StarterPartitionKey
            );
        }

        private void GenerateStarterWorld()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
        }

        private static ContentCatalogBuildResult CreateValidBuildResult()
        {
            var regions = new Dictionary<string, RegionDefinition>
            {
                {
                    "parent_region",
                    new RegionDefinition
                    {
                        key = "parent_region",
                        displayName = "Parent Region",
                        type = "region",
                        tickIntervalSeconds = 180f
                    }
                },
                {
                    "child_region",
                    new RegionDefinition
                    {
                        key = "child_region",
                        displayName = "Child Region",
                        type = "castle",
                        parentKey = "parent_region",
                        tickIntervalSeconds = 120f
                    }
                }
            };
            var entityTemplates = new Dictionary<string, EntityTemplateDefinition>
            {
                {
                    "hero",
                    new EntityTemplateDefinition
                    {
                        key = "hero",
                        displayName = "Hero",
                        raceKey = "human",
                        subraceKey = "standard_human",
                        gender = "F",
                        houseKey = "house_valemont",
                        role = "heir",
                        regionKey = "child_region",
                        age = 30,
                        traitKeys = new[] { "strong" },
                        magicKeys = Array.Empty<string>(),
                        factions = Array.Empty<EntityFactionMembershipDefinition>(),
                        notes = "Test entity."
                    }
                }
            };
            var catalog = new ContentCatalog(
                new Dictionary<string, FactionDefinition>(),
                new Dictionary<string, RaceDefinition>(),
                new Dictionary<string, StatDefinition>(),
                new Dictionary<string, TraitDefinition>(),
                regions,
                entityTemplates
            );

            return new ContentCatalogBuildResult(catalog, ContentValidationResult.Success);
        }
    }
}
