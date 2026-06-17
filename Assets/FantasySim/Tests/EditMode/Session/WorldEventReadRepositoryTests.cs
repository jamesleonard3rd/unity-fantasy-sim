using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Persistence.SQLite;
using FantasySim.Simulation.State;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Session
{
    public sealed class WorldEventReadRepositoryTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;
        private long regionId;
        private long entityId;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimWorldEventReadTests_{Guid.NewGuid():N}");
            databasePath = Path.Combine(tempDirectory, DatabasePathProvider.DatabaseFileName);
            pathProvider = new DatabasePathProvider(databasePath);

            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(LiveRegionTestWorld.CreateValidBuildResult());

            LoadedPartitionState state = new StarterWorldReadRepository(pathProvider).LoadStarterWorld();
            EntityState entity = state.Entities.First();
            entityId = entity.Id.Value;
            regionId = entity.RegionId.Value;

            SeedEvents();
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
        public void QueryOrdersByTickDescendingAndRespectsLimit()
        {
            var repository = new WorldEventReadRepository(pathProvider);

            IReadOnlyList<WorldEventRecord> results = repository.Query(new WorldEventQuery { Limit = 2 });

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].OccurredTick.Value, Is.GreaterThanOrEqualTo(results[1].OccurredTick.Value));
        }

        [Test]
        public void QueryFiltersByTypeAndSignificance()
        {
            var repository = new WorldEventReadRepository(pathProvider);

            IReadOnlyList<WorldEventRecord> byType = repository.Query(new WorldEventQuery
            {
                Type = "relationship.opinion_changed"
            });
            Assert.That(byType, Is.Not.Empty);
            Assert.That(byType.All(record => record.Type == "relationship.opinion_changed"), Is.True);

            IReadOnlyList<WorldEventRecord> significant = repository.Query(new WorldEventQuery
            {
                MinSignificance = 2
            });
            Assert.That(significant, Is.Not.Empty);
            Assert.That(significant.All(record => record.Significance >= 2), Is.True);
        }

        [Test]
        public void QueryFiltersByEntityAndTickRange()
        {
            var repository = new WorldEventReadRepository(pathProvider);

            IReadOnlyList<WorldEventRecord> byEntity = repository.Query(new WorldEventQuery
            {
                EntityId = new EntityId(entityId)
            });
            Assert.That(byEntity, Is.Not.Empty);
            Assert.That(
                byEntity.All(record =>
                    (record.SubjectEntityId.HasValue && record.SubjectEntityId.Value.Value == entityId)
                    || (record.TargetEntityId.HasValue && record.TargetEntityId.Value.Value == entityId)),
                Is.True
            );

            IReadOnlyList<WorldEventRecord> ranged = repository.Query(new WorldEventQuery
            {
                MinTick = new GameTick(2),
                MaxTick = new GameTick(3)
            });
            Assert.That(ranged, Is.Not.Empty);
            Assert.That(ranged.All(record => record.OccurredTick.Value >= 2 && record.OccurredTick.Value <= 3), Is.True);
        }

        private void SeedEvents()
        {
            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            InsertEvent(connection, "region.tick", 1, 1, null, null);
            InsertEvent(connection, "interaction.occurred", 1, 2, entityId, entityId);
            InsertEvent(connection, "goal.primary_selected", 2, 3, entityId, null);
            InsertEvent(connection, "relationship.opinion_changed", 4, 4, entityId, entityId);
        }

        private void InsertEvent(
            SQLiteConnection connection,
            string type,
            int significance,
            long tick,
            long? subjectEntityId,
            long? targetEntityId
        )
        {
            connection.Execute(
                @"INSERT INTO world_events (
                    region_id, type, subject_entity_id, target_entity_id,
                    significance, payload_json, occurred_game_tick, created_utc
                  ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                regionId,
                type,
                subjectEntityId.HasValue ? (object)subjectEntityId.Value : null,
                targetEntityId.HasValue ? (object)targetEntityId.Value : null,
                significance,
                "{}",
                tick,
                DateTime.UtcNow.ToString("O")
            );
        }
    }
}
