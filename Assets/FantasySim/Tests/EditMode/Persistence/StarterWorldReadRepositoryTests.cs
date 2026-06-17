using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using FantasySim.Persistence.SQLite;
using FantasySim.Simulation.State;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Persistence
{
    public sealed class StarterWorldReadRepositoryTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimStarterWorldReadTests_{Guid.NewGuid():N}");
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
        public void LoadStarterWorldHydratesGeneratedPartition()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            var repository = new StarterWorldReadRepository(pathProvider);

            LoadedPartitionState state = repository.LoadStarterWorld();

            Assert.That(state.TickContext.CurrentTick.Value, Is.EqualTo(0));
            Assert.That(state.TickContext.TicksPerGameDay, Is.EqualTo(24));
            Assert.That(state.TickContext.PartitionKey, Is.EqualTo(StarterWorldReadRepository.StarterPartitionKey));
            Assert.That(state.Regions, Has.Count.EqualTo(2));
            Assert.That(state.Entities, Has.Count.EqualTo(1));

            RegionState parentRegion = state.Regions.Single(region => region.Key == "parent_region");
            RegionState childRegion = state.Regions.Single(region => region.Key == "child_region");
            Assert.That(childRegion.ParentRegionId.HasValue, Is.True);
            Assert.That(childRegion.ParentRegionId.Value.Value, Is.EqualTo(parentRegion.Id.Value));

            EntityState entity = state.Entities.Single();
            Assert.That(entity.TemplateKey, Is.EqualTo("hero"));
            Assert.That(entity.RegionId.Value, Is.EqualTo(childRegion.Id.Value));
            Assert.That(entity.AgeTicks, Is.EqualTo(30L * 365L * 24L));
        }

        [Test]
        public void LoadStarterWorldFailsWhenGeneratedStateIsMissing()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.Initialize();
            var repository = new StarterWorldReadRepository(pathProvider);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => repository.LoadStarterWorld()
            );

            Assert.That(exception.Message, Does.Contain("world_clock"));
        }

        [Test]
        public void LoadPartitionFailsWhenPartitionKeyIsBlank()
        {
            var repository = new StarterWorldReadRepository(pathProvider);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => repository.LoadPartition(" ")
            );

            Assert.That(exception.ParamName, Is.EqualTo("partitionKey"));
        }

        [Test]
        public void LoadPartitionFailsWhenPartitionIsMissing()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            var repository = new StarterWorldReadRepository(pathProvider);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => repository.LoadPartition("missing_partition")
            );

            Assert.That(exception.Message, Does.Contain("missing_partition"));
        }

        [Test]
        public void LoadStarterWorldFiltersEntitiesOutsideStarterPartition()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());

            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                connection.Execute(
                    @"INSERT INTO regions (key, display_name, type, tick_interval_seconds)
                      VALUES (?, ?, ?, ?)",
                    "outside_region",
                    "Outside Region",
                    "region",
                    180f
                );
                long outsideRegionId = connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

                connection.Execute(
                    @"INSERT INTO entities (template_key, display_name, race_key, subrace_key, age_ticks)
                      VALUES (?, ?, ?, ?, ?)",
                    "outsider",
                    "Outsider",
                    "human",
                    "standard_human",
                    10L
                );
                long outsideEntityId = connection.ExecuteScalar<long>("SELECT last_insert_rowid()");
                connection.Execute(
                    "INSERT INTO entity_positions (entity_id, region_id) VALUES (?, ?)",
                    outsideEntityId,
                    outsideRegionId
                );
            }

            var repository = new StarterWorldReadRepository(pathProvider);

            LoadedPartitionState state = repository.LoadStarterWorld();

            Assert.That(state.Regions.Select(region => region.Key), Does.Not.Contain("outside_region"));
            Assert.That(state.Entities.Select(entity => entity.TemplateKey), Does.Not.Contain("outsider"));
            Assert.That(state.Entities, Has.Count.EqualTo(1));
        }

        [Test]
        public void LoadStarterWorldHydratesRelationshipsAndTermsForLoadedEntities()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                long childRegionId = connection.ExecuteScalar<long>("SELECT id FROM regions WHERE key = ?", "child_region");
                long heroId = connection.ExecuteScalar<long>("SELECT id FROM entities WHERE template_key = ?", "hero");
                long allyId = InsertEntity(connection, "ally", "Ally", childRegionId);
                connection.Execute(
                    @"INSERT INTO relationships (
                        subject_entity_id,
                        target_entity_id,
                        opinion,
                        familiarity,
                        updated_game_tick
                      )
                      VALUES (?, ?, ?, ?, ?)",
                    heroId,
                    allyId,
                    12,
                    4,
                    7L
                );
                connection.Execute(
                    @"INSERT INTO relationship_terms (
                        subject_entity_id,
                        target_entity_id,
                        type,
                        value,
                        decay_per_tick,
                        expires_game_tick,
                        payload_json
                      )
                      VALUES (?, ?, ?, ?, ?, ?, ?)",
                    heroId,
                    allyId,
                    "favor",
                    12,
                    0.5,
                    20L,
                    "{\"source\":\"test\"}"
                );
            }
            var repository = new StarterWorldReadRepository(pathProvider);

            LoadedPartitionState state = repository.LoadStarterWorld();

            Assert.That(state.Relationships, Has.Count.EqualTo(1));
            RelationshipState relationship = state.Relationships.Single();
            Assert.That(relationship.Key.SubjectEntityId.Value, Is.EqualTo(state.Entities.Single(entity => entity.TemplateKey == "hero").Id.Value));
            Assert.That(relationship.Key.TargetEntityId.Value, Is.EqualTo(state.Entities.Single(entity => entity.TemplateKey == "ally").Id.Value));
            Assert.That(relationship.Opinion, Is.EqualTo(12));
            Assert.That(relationship.Familiarity, Is.EqualTo(4));
            Assert.That(relationship.UpdatedTick.Value, Is.EqualTo(7L));

            Assert.That(state.RelationshipTerms, Has.Count.EqualTo(1));
            RelationshipTermState term = state.RelationshipTerms.Single();
            Assert.That(term.Key.SubjectEntityId.Value, Is.EqualTo(relationship.Key.SubjectEntityId.Value));
            Assert.That(term.Key.TargetEntityId.Value, Is.EqualTo(relationship.Key.TargetEntityId.Value));
            Assert.That(term.Type, Is.EqualTo("favor"));
            Assert.That(term.Value, Is.EqualTo(12));
            Assert.That(term.DecayPerTick, Is.EqualTo(0.5));
            Assert.That(term.ExpiresTick.HasValue, Is.True);
            Assert.That(term.ExpiresTick.Value.Value, Is.EqualTo(20L));
            Assert.That(term.PayloadJson, Is.EqualTo("{\"source\":\"test\"}"));
        }

        [Test]
        public void LoadStarterWorldFiltersRelationshipsWhenEitherEntityIsOutsidePartition()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                long childRegionId = connection.ExecuteScalar<long>("SELECT id FROM regions WHERE key = ?", "child_region");
                long heroId = connection.ExecuteScalar<long>("SELECT id FROM entities WHERE template_key = ?", "hero");
                long allyId = InsertEntity(connection, "ally", "Ally", childRegionId);
                connection.Execute(
                    @"INSERT INTO regions (key, display_name, type, tick_interval_seconds)
                      VALUES (?, ?, ?, ?)",
                    "outside_region",
                    "Outside Region",
                    "region",
                    180f
                );
                long outsideRegionId = connection.ExecuteScalar<long>("SELECT last_insert_rowid()");
                long outsiderId = InsertEntity(connection, "outsider", "Outsider", outsideRegionId);

                InsertRelationship(connection, heroId, allyId, 5);
                InsertRelationship(connection, heroId, outsiderId, 90);
                InsertRelationship(connection, outsiderId, heroId, -90);
                InsertRelationshipTerm(connection, heroId, allyId, 5);
                InsertRelationshipTerm(connection, heroId, outsiderId, 90);
                InsertRelationshipTerm(connection, outsiderId, heroId, -90);
            }
            var repository = new StarterWorldReadRepository(pathProvider);

            LoadedPartitionState state = repository.LoadStarterWorld();

            Assert.That(state.Relationships, Has.Count.EqualTo(1));
            Assert.That(state.RelationshipTerms, Has.Count.EqualTo(1));
            Assert.That(state.Relationships.Single().Opinion, Is.EqualTo(5));
            Assert.That(state.RelationshipTerms.Single().Value, Is.EqualTo(5));
        }

        [Test]
        public void LoadStarterWorldPreservesNullablePositions()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            var repository = new StarterWorldReadRepository(pathProvider);

            EntityState entity = repository.LoadStarterWorld().Entities.Single();

            Assert.That(entity.LocalX.HasValue, Is.False);
            Assert.That(entity.LocalY.HasValue, Is.False);
            Assert.That(entity.LocalZ.HasValue, Is.False);
            Assert.That(entity.HeadingY.HasValue, Is.False);
        }

        [Test]
        public void LoadStarterWorldFailsWhenEntityPositionIsMissing()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                connection.Execute("DELETE FROM entity_positions");
            }
            var repository = new StarterWorldReadRepository(pathProvider);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => repository.LoadStarterWorld()
            );

            Assert.That(exception.Message, Does.Contain("missing a position"));
        }

        [Test]
        public void LoadStarterWorldHydratesMutableSaveStateColumns()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                long childRegionId = connection.ExecuteScalar<long>(
                    "SELECT id FROM regions WHERE key = ?",
                    "child_region"
                );
                long entityId = connection.ExecuteScalar<long>(
                    "SELECT id FROM entities WHERE template_key = ?",
                    "hero"
                );
                connection.Execute(
                    "UPDATE world_clock SET game_tick = ? WHERE id = 1",
                    48L
                );
                connection.Execute(
                    "UPDATE simulation_partitions SET last_tick_game_tick = ? WHERE key = ?",
                    24L,
                    StarterWorldReadRepository.StarterPartitionKey
                );
                connection.Execute(
                    @"UPDATE regions
                      SET authority_mode = ?, authority_owner = ?, authority_until_tick = ?
                      WHERE id = ?",
                    "live",
                    "player",
                    72L,
                    childRegionId
                );
                connection.Execute(
                    "UPDATE entities SET is_alive = ?, payload_json = ? WHERE id = ?",
                    0,
                    "{\"custom\":true}",
                    entityId
                );
            }
            var repository = new StarterWorldReadRepository(pathProvider);

            LoadedPartitionState state = repository.LoadStarterWorld();

            Assert.That(state.TickContext.CurrentTick.Value, Is.EqualTo(48L));
            Assert.That(state.TickContext.PartitionLastTick.Value, Is.EqualTo(24L));

            RegionState childRegion = state.Regions.Single(region => region.Key == "child_region");
            Assert.That(childRegion.AuthorityMode, Is.EqualTo("live"));
            Assert.That(childRegion.AuthorityOwner, Is.EqualTo("player"));
            Assert.That(childRegion.AuthorityUntilTick.HasValue, Is.True);
            Assert.That(childRegion.AuthorityUntilTick.Value.Value, Is.EqualTo(72L));

            EntityState entity = state.Entities.Single();
            Assert.That(entity.IsAlive, Is.False);
            Assert.That(entity.PayloadJson, Is.EqualTo("{\"custom\":true}"));
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

        private static long InsertEntity(SQLiteConnection connection, string templateKey, string displayName, long regionId)
        {
            connection.Execute(
                @"INSERT INTO entities (template_key, display_name, race_key, subrace_key, age_ticks)
                  VALUES (?, ?, ?, ?, ?)",
                templateKey,
                displayName,
                "human",
                "standard_human",
                10L
            );
            long entityId = connection.ExecuteScalar<long>("SELECT last_insert_rowid()");
            connection.Execute(
                "INSERT INTO entity_positions (entity_id, region_id) VALUES (?, ?)",
                entityId,
                regionId
            );
            return entityId;
        }

        private static void InsertRelationship(SQLiteConnection connection, long subjectId, long targetId, int opinion)
        {
            connection.Execute(
                @"INSERT INTO relationships (
                    subject_entity_id,
                    target_entity_id,
                    opinion,
                    familiarity,
                    updated_game_tick
                  )
                  VALUES (?, ?, ?, ?, ?)",
                subjectId,
                targetId,
                opinion,
                0,
                0L
            );
        }

        private static void InsertRelationshipTerm(SQLiteConnection connection, long subjectId, long targetId, int value)
        {
            connection.Execute(
                @"INSERT INTO relationship_terms (
                    subject_entity_id,
                    target_entity_id,
                    type,
                    value,
                    decay_per_tick,
                    expires_game_tick,
                    payload_json
                  )
                  VALUES (?, ?, ?, ?, ?, NULL, ?)",
                subjectId,
                targetId,
                "test",
                value,
                0,
                "{}"
            );
        }
    }
}
