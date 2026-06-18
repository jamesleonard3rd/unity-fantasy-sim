using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Content.Validation;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Persistence.SQLite;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Persistence
{
    public sealed class RegionStateWriteRepositoryTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimRegionStateWriteTests_{Guid.NewGuid():N}");
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
        public void WritePartitionTickPersistsHeartbeatEventsAndAdvancesWorldClock()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            var readRepository = new StarterWorldReadRepository(pathProvider);
            LoadedPartitionState state = readRepository.LoadStarterWorld();
            PartitionTickDelta delta = new StarterWorldSimulationPipeline().Tick(state);
            var writeRepository = new RegionStateWriteRepository(pathProvider);

            RegionStateWriteResult result = writeRepository.WritePartitionTick(state, delta);

            Assert.That(result.PreviousTick.Value, Is.EqualTo(0));
            Assert.That(result.CurrentTick.Value, Is.EqualTo(1));
            Assert.That(result.EventsInserted, Is.EqualTo(2));

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(connection.ExecuteScalar<long>("SELECT game_tick FROM world_clock WHERE id = 1"), Is.EqualTo(1));
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_events"), Is.EqualTo(2));
            Assert.That(
                connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_events WHERE type = ?", RegionHeartbeatSystem.EventType),
                Is.EqualTo(2)
            );
            Assert.That(
                connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_events WHERE significance = ?", RegionHeartbeatSystem.EventSignificance),
                Is.EqualTo(2)
            );
            Assert.That(
                connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_events WHERE payload_json = ?", RegionHeartbeatSystem.EventPayloadJson),
                Is.EqualTo(2)
            );
            Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_events WHERE occurred_game_tick = 1"), Is.EqualTo(2));

            LoadedPartitionState reloaded = readRepository.LoadStarterWorld();
            Assert.That(reloaded.TickContext.CurrentTick.Value, Is.EqualTo(1));
        }

        [Test]
        public void WritePartitionTickRejectsNonAdvancingTick()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            LoadedPartitionState state = new StarterWorldReadRepository(pathProvider).LoadStarterWorld();
            var delta = new PartitionTickDelta(
                state.TickContext.CurrentTick,
                state.TickContext.CurrentTick,
                new RegionStateDelta[0]
            );
            var writeRepository = new RegionStateWriteRepository(pathProvider);

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => writeRepository.WritePartitionTick(state, delta)
            );

            Assert.That(exception.ParamName, Is.EqualTo("delta"));
        }

        [Test]
        public void WritePartitionTickPersistsChangedRelationshipRowsOnly()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            long changedTermId;
            long unchangedTermId;
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                long childRegionId = connection.ExecuteScalar<long>("SELECT id FROM regions WHERE key = ?", "child_region");
                long heroId = connection.ExecuteScalar<long>("SELECT id FROM entities WHERE template_key = ?", "hero");
                long allyId = InsertEntity(connection, "ally", "Ally", childRegionId);
                InsertRelationship(connection, heroId, allyId, 8, 2, 0L);
                changedTermId = InsertRelationshipTerm(connection, heroId, allyId, "favor", 8, 1.0, null, "{}");
                unchangedTermId = InsertRelationshipTerm(connection, heroId, allyId, "kinship", 3, 0.0, null, "{}");
            }
            var readRepository = new StarterWorldReadRepository(pathProvider);
            LoadedPartitionState state = readRepository.LoadStarterWorld();
            RegionState region = state.Regions.Single(regionState => regionState.Key == "child_region");
            EntityState hero = state.Entities.Single(entity => entity.TemplateKey == "hero");
            EntityState ally = state.Entities.Single(entity => entity.TemplateKey == "ally");
            var key = new RelationshipKey(hero.Id, ally.Id);
            var delta = new PartitionTickDelta(
                state.TickContext.CurrentTick,
                new GameTick(1),
                new[]
                {
                    new RegionStateDelta(
                        region.Id,
                        new GameTick(1),
                        new PendingWorldEvent[0],
                        new[]
                        {
                            new RelationshipState(key, 22, 5, new GameTick(1))
                        },
                        new[]
                        {
                            new RelationshipTermState(changedTermId, key, "favor", 21, 1.0, null, "{}")
                        }
                    )
                }
            );
            var writeRepository = new RegionStateWriteRepository(pathProvider);

            RegionStateWriteResult result = writeRepository.WritePartitionTick(state, delta);

            Assert.That(result.RelationshipsUpdated, Is.EqualTo(1));
            Assert.That(result.RelationshipTermsUpdated, Is.EqualTo(1));
            Assert.That(result.RelationshipTermsDeleted, Is.EqualTo(0));
            using SQLiteConnection assertionConnection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(
                assertionConnection.ExecuteScalar<int>(
                    @"SELECT opinion FROM relationships
                      WHERE subject_entity_id = ? AND target_entity_id = ?",
                    hero.Id.Value,
                    ally.Id.Value
                ),
                Is.EqualTo(22)
            );
            Assert.That(
                assertionConnection.ExecuteScalar<int>("SELECT familiarity FROM relationships WHERE subject_entity_id = ? AND target_entity_id = ?", hero.Id.Value, ally.Id.Value),
                Is.EqualTo(5)
            );
            Assert.That(assertionConnection.ExecuteScalar<int>("SELECT value FROM relationship_terms WHERE id = ?", changedTermId), Is.EqualTo(21));
            Assert.That(assertionConnection.ExecuteScalar<int>("SELECT value FROM relationship_terms WHERE id = ?", unchangedTermId), Is.EqualTo(3));
        }

        [Test]
        public void WritePartitionTickDeletesExpiredRelationshipTerms()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            long termId;
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                long childRegionId = connection.ExecuteScalar<long>("SELECT id FROM regions WHERE key = ?", "child_region");
                long heroId = connection.ExecuteScalar<long>("SELECT id FROM entities WHERE template_key = ?", "hero");
                long allyId = InsertEntity(connection, "ally", "Ally", childRegionId);
                InsertRelationship(connection, heroId, allyId, 8, 2, 0L);
                termId = InsertRelationshipTerm(connection, heroId, allyId, "favor", 8, 1.0, 1L, "{}");
            }
            var readRepository = new StarterWorldReadRepository(pathProvider);
            LoadedPartitionState state = readRepository.LoadStarterWorld();
            RegionState region = state.Regions.Single(regionState => regionState.Key == "child_region");
            var delta = new PartitionTickDelta(
                state.TickContext.CurrentTick,
                new GameTick(1),
                new[]
                {
                    new RegionStateDelta(
                        region.Id,
                        new GameTick(1),
                        new PendingWorldEvent[0],
                        null,
                        null,
                        new[] { termId }
                    )
                }
            );
            var writeRepository = new RegionStateWriteRepository(pathProvider);

            RegionStateWriteResult result = writeRepository.WritePartitionTick(state, delta);

            Assert.That(result.RelationshipTermsDeleted, Is.EqualTo(1));
            using SQLiteConnection assertionConnection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(assertionConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM relationship_terms WHERE id = ?", termId), Is.EqualTo(0));
        }

        [Test]
        public void WritePartitionTickPersistsChangedEntityGoalRowsOnly()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            long changedGoalId;
            long unchangedGoalId;
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                long heroId = connection.ExecuteScalar<long>("SELECT id FROM entities WHERE template_key = ?", "hero");
                changedGoalId = InsertGoal(connection, heroId, "join_faction", 0.0, "inactive");
                unchangedGoalId = InsertGoal(connection, heroId, "travel_to_region", 10.0, "inactive");
            }
            var readRepository = new StarterWorldReadRepository(pathProvider);
            LoadedPartitionState state = readRepository.LoadStarterWorld();
            RegionState region = state.Regions.Single(regionState => regionState.Key == "child_region");
            EntityState hero = state.Entities.Single(entity => entity.TemplateKey == "hero");
            var delta = new PartitionTickDelta(
                state.TickContext.CurrentTick,
                new GameTick(1),
                new[]
                {
                    new RegionStateDelta(
                        region.Id,
                        new GameTick(1),
                        new PendingWorldEvent[0],
                        null,
                        null,
                        null,
                        new[]
                        {
                            new EntityGoalState(changedGoalId, hero.Id, "join_faction", 60, "active", "{}")
                        },
                        null
                    )
                }
            );
            var writeRepository = new RegionStateWriteRepository(pathProvider);

            RegionStateWriteResult result = writeRepository.WritePartitionTick(state, delta);

            Assert.That(result.EntityGoalsUpdated, Is.EqualTo(1));
            Assert.That(result.EntityGoalsDeleted, Is.EqualTo(0));
            using SQLiteConnection assertionConnection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(assertionConnection.ExecuteScalar<string>("SELECT state FROM entity_goals WHERE id = ?", changedGoalId), Is.EqualTo("active"));
            Assert.That(assertionConnection.ExecuteScalar<int>("SELECT priority FROM entity_goals WHERE id = ?", changedGoalId), Is.EqualTo(60));
            Assert.That(assertionConnection.ExecuteScalar<string>("SELECT state FROM entity_goals WHERE id = ?", unchangedGoalId), Is.EqualTo("inactive"));
            Assert.That(assertionConnection.ExecuteScalar<int>("SELECT priority FROM entity_goals WHERE id = ?", unchangedGoalId), Is.EqualTo(10));
        }

        [Test]
        public void WritePartitionTickRelocatesEntityToTargetRegionByKey()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(CreateValidBuildResult());
            var readRepository = new StarterWorldReadRepository(pathProvider);
            LoadedPartitionState state = readRepository.LoadStarterWorld();
            RegionState childRegion = state.Regions.Single(regionState => regionState.Key == "child_region");
            EntityState hero = state.Entities.Single(entity => entity.TemplateKey == "hero");
            long parentRegionId;
            using (SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection())
            {
                parentRegionId = connection.ExecuteScalar<long>("SELECT id FROM regions WHERE key = ?", "parent_region");
            }

            var delta = new PartitionTickDelta(
                state.TickContext.CurrentTick,
                new GameTick(1),
                new[]
                {
                    new RegionStateDelta(
                        childRegion.Id,
                        new GameTick(1),
                        new PendingWorldEvent[0],
                        null,
                        null,
                        null,
                        null,
                        null,
                        new[] { new EntityRegionMove(hero.Id, "parent_region") }
                    )
                }
            );
            var writeRepository = new RegionStateWriteRepository(pathProvider);

            RegionStateWriteResult result = writeRepository.WritePartitionTick(state, delta);

            Assert.That(result.EntityPositionsUpdated, Is.EqualTo(1));
            using SQLiteConnection assertionConnection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            Assert.That(
                assertionConnection.ExecuteScalar<long>("SELECT region_id FROM entity_positions WHERE entity_id = ?", hero.Id.Value),
                Is.EqualTo(parentRegionId)
            );
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

        private static void InsertRelationship(
            SQLiteConnection connection,
            long subjectId,
            long targetId,
            int opinion,
            int familiarity,
            long updatedTick
        )
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
                familiarity,
                updatedTick
            );
        }

        private static long InsertRelationshipTerm(
            SQLiteConnection connection,
            long subjectId,
            long targetId,
            string type,
            int value,
            double decayPerTick,
            long? expiresTick,
            string payloadJson
        )
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
                  VALUES (?, ?, ?, ?, ?, ?, ?)",
                subjectId,
                targetId,
                type,
                value,
                decayPerTick,
                expiresTick.HasValue ? (object)expiresTick.Value : null,
                payloadJson
            );
            return connection.ExecuteScalar<long>("SELECT last_insert_rowid()");
        }

        private static long InsertGoal(
            SQLiteConnection connection,
            long entityId,
            string goalKey,
            double priority,
            string state
        )
        {
            connection.Execute(
                @"INSERT INTO entity_goals (entity_id, goal_key, priority, state, payload_json)
                  VALUES (?, ?, ?, ?, ?)",
                entityId,
                goalKey,
                priority,
                state,
                "{}"
            );
            return connection.ExecuteScalar<long>("SELECT last_insert_rowid()");
        }
    }
}
