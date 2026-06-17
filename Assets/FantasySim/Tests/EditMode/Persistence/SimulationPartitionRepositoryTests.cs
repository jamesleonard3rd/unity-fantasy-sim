using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using FantasySim.Persistence.SQLite;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Persistence
{
    public sealed class SimulationPartitionRepositoryTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimPartitionRepoTests_{Guid.NewGuid():N}");
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
        public void LoadCoarsePartitionsReturnsGeneratedStarterPartition()
        {
            GenerateStarterWorld();
            var repository = new SimulationPartitionRepository(pathProvider);

            IReadOnlyList<CoarsePartitionInfo> partitions = repository.LoadCoarsePartitions();

            CoarsePartitionInfo starter = partitions.Single();
            Assert.That(starter.Key, Is.EqualTo(StarterWorldReadRepository.StarterPartitionKey));
            Assert.That(starter.Id, Is.GreaterThan(0));
            Assert.That(starter.TickIntervalSeconds, Is.EqualTo(180f));
            Assert.That(starter.LastTickGameTick, Is.EqualTo(0));
        }

        [Test]
        public void LoadCoarsePartitionsExcludesPartitionsMarkedLive()
        {
            GenerateStarterWorld();
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                connection.Execute(
                    "UPDATE simulation_partitions SET authority_mode = ? WHERE key = ?",
                    SimulationPartitionRepository.LiveAuthorityMode,
                    StarterWorldReadRepository.StarterPartitionKey
                );
            }

            var repository = new SimulationPartitionRepository(pathProvider);

            Assert.That(repository.LoadCoarsePartitions(), Is.Empty);
        }

        [Test]
        public void LoadCoarsePartitionsExcludesPartitionsContainingLiveRegion()
        {
            GenerateStarterWorld();
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                connection.Execute(
                    "UPDATE regions SET authority_mode = ? WHERE key = ?",
                    SimulationPartitionRepository.LiveAuthorityMode,
                    "child_region"
                );
            }

            var repository = new SimulationPartitionRepository(pathProvider);

            Assert.That(repository.LoadCoarsePartitions(), Is.Empty);
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
