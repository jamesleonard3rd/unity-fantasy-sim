using System;
using System.Collections.Generic;
using System.IO;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using FantasySim.Core.Results;
using FantasySim.Persistence.SQLite;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Persistence
{
    public sealed class StarterWorldGeneratorTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimStarterWorldTests_{Guid.NewGuid():N}");
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
        public void GenerateStarterWorldIfEmptyWritesInitialSaveState()
        {
            var initializer = new DatabaseInitializer(pathProvider);

            StarterWorldGenerationResult result = initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());

            Assert.That(result.Generated, Is.True);
            Assert.That(result.RegionsInserted, Is.EqualTo(2));
            Assert.That(result.PartitionsInserted, Is.EqualTo(1));
            Assert.That(result.EntitiesInserted, Is.EqualTo(1));

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_clock"), Is.EqualTo(1));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM regions"), Is.EqualTo(2));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM simulation_partitions"), Is.EqualTo(1));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM simulation_partition_regions"), Is.EqualTo(2));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM entities"), Is.EqualTo(1));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM entity_positions"), Is.EqualTo(1));
            Assert.That(connection.ExecuteScalar<string>("SELECT template_key FROM entities"), Is.EqualTo("hero"));
            Assert.That(connection.ExecuteScalar<long>("SELECT age_ticks FROM entities"), Is.EqualTo(30L * 365L * 24L));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM regions WHERE parent_id IS NOT NULL"), Is.EqualTo(1));
        }

        [Test]
        public void GenerateStarterWorldIfEmptyIsIdempotent()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());

            StarterWorldGenerationResult secondRun = initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());

            Assert.That(secondRun.Generated, Is.False);
            Assert.That(secondRun.Message, Does.Contain("skipped"));

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM regions"), Is.EqualTo(2));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM entities"), Is.EqualTo(1));
        }

        [Test]
        public void GenerateStarterWorldIfEmptyRejectsInvalidCatalog()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            var result = new ContentCatalogBuildResult(
                ContentCatalog.Empty,
                new ContentValidationResult(new[]
                {
                    new ValidationMessage(ValidationSeverity.Error, "content.invalid_reference", "Missing region.")
                })
            );

            Assert.Throws<InvalidOperationException>(() => initializer.GenerateStarterWorldIfEmpty(result));

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_clock"), Is.EqualTo(0));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM regions"), Is.EqualTo(0));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM entities"), Is.EqualTo(0));
        }

        [Test]
        public void GenerateStarterWorldSeedsKinshipAndDerivedHouseTermsWithSummedOpinions()
        {
            var initializer = new DatabaseInitializer(pathProvider);

            StarterWorldGenerationResult result = initializer.GenerateStarterWorldIfEmpty(CreateSocialBuildResult());

            Assert.That(result.Generated, Is.True);
            Assert.That(result.RelationshipTermsInserted, Is.EqualTo(4));
            Assert.That(result.RelationshipsInserted, Is.EqualTo(2));
            Assert.That(result.EntityGoalsInserted, Is.EqualTo(4));

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM relationship_terms WHERE type = 'kinship'"), Is.EqualTo(2));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM relationship_terms WHERE type = 'house'"), Is.EqualTo(2));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM relationships"), Is.EqualTo(2));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM entity_goals"), Is.EqualTo(4));

            Assert.That(OpinionBetween(connection, "lord", "heir"), Is.EqualTo(85));
            Assert.That(OpinionBetween(connection, "heir", "lord"), Is.EqualTo(80));
            Assert.That(
                connection.ExecuteScalar<int>("SELECT COUNT(*) FROM entity_goals WHERE state <> 'inactive'"),
                Is.EqualTo(0)
            );
        }

        private static int OpinionBetween(SQLiteConnection connection, string subjectTemplateKey, string targetTemplateKey)
        {
            return connection.ExecuteScalar<int>(
                @"SELECT r.opinion
                  FROM relationships r
                  INNER JOIN entities s ON s.id = r.subject_entity_id
                  INNER JOIN entities t ON t.id = r.target_entity_id
                  WHERE s.template_key = ? AND t.template_key = ?",
                subjectTemplateKey,
                targetTemplateKey
            );
        }

        private static ContentCatalogBuildResult CreateSocialBuildResult()
        {
            var regions = new Dictionary<string, RegionDefinition>
            {
                {
                    "child_region",
                    new RegionDefinition
                    {
                        key = "child_region",
                        displayName = "Child Region",
                        type = "castle",
                        tickIntervalSeconds = 120f
                    }
                }
            };
            var entityTemplates = new Dictionary<string, EntityTemplateDefinition>
            {
                {
                    "lord",
                    new EntityTemplateDefinition
                    {
                        key = "lord",
                        displayName = "Lord",
                        raceKey = "human",
                        houseKey = "house_test",
                        role = "patriarch",
                        regionKey = "child_region",
                        age = 50,
                        traitKeys = Array.Empty<string>(),
                        magicKeys = Array.Empty<string>(),
                        factions = Array.Empty<EntityFactionMembershipDefinition>()
                    }
                },
                {
                    "heir",
                    new EntityTemplateDefinition
                    {
                        key = "heir",
                        displayName = "Heir",
                        raceKey = "human",
                        houseKey = "house_test",
                        role = "heir",
                        regionKey = "child_region",
                        age = 25,
                        traitKeys = Array.Empty<string>(),
                        magicKeys = Array.Empty<string>(),
                        factions = Array.Empty<EntityFactionMembershipDefinition>()
                    }
                }
            };
            var goals = new Dictionary<string, GoalDefinition>
            {
                { "travel_to_region", new GoalDefinition { key = "travel_to_region", displayName = "travel_to_region" } },
                { "join_faction", new GoalDefinition { key = "join_faction", displayName = "join_faction" } }
            };
            var entityRelationships = new[]
            {
                new EntityRelationshipDefinition { fromKey = "lord", toKey = "heir", type = "parent", opinion = 70 },
                new EntityRelationshipDefinition { fromKey = "heir", toKey = "lord", type = "child", opinion = 65 }
            };
            var catalog = new ContentCatalog(
                new Dictionary<string, FactionDefinition>(),
                new Dictionary<string, RaceDefinition>(),
                new Dictionary<string, StatDefinition>(),
                new Dictionary<string, TraitDefinition>(),
                regions,
                entityTemplates,
                goals,
                entityRelationships
            );

            return new ContentCatalogBuildResult(catalog, ContentValidationResult.Success);
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
