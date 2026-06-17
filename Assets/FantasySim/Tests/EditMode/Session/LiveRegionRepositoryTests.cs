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
using FantasySim.Simulation.State;
using NUnit.Framework;
using SQLite;

namespace FantasySim.Tests.EditMode.Session
{
    public sealed class LiveRegionRepositoryTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimLiveRegionRepoTests_{Guid.NewGuid():N}");
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
        public void SetRegionAuthorityUpdatesRegionRow()
        {
            GenerateStarterWorld();
            RegionState region = LoadFirstRegion();
            var repository = new LiveRegionRepository(pathProvider);

            repository.SetRegionAuthority(region.Id, LiveRegionRepository.LiveAuthorityMode, "player_session");

            using SQLiteConnection connection = new SqliteConnectionFactory(pathProvider).OpenConnection();
            AuthorityRow row = connection.Query<AuthorityRow>(
                "SELECT authority_mode AS Mode, authority_owner AS Owner FROM regions WHERE id = ?",
                region.Id.Value
            ).Single();

            Assert.That(row.Mode, Is.EqualTo(LiveRegionRepository.LiveAuthorityMode));
            Assert.That(row.Owner, Is.EqualTo("player_session"));
        }

        [Test]
        public void WriteEntityPositionsPersistsCoordinates()
        {
            GenerateStarterWorld();
            EntityState entity = LoadFirstEntity();
            var repository = new LiveRegionRepository(pathProvider);

            int written = repository.WriteEntityPositions(new[]
            {
                new EntityPositionUpdate(entity.Id, entity.RegionId, 12.5f, 1f, -7.25f, 90f)
            });

            Assert.That(written, Is.EqualTo(1));

            EntityState reloaded = LoadFirstEntity();
            Assert.That(reloaded.LocalX, Is.EqualTo(12.5f));
            Assert.That(reloaded.LocalY, Is.EqualTo(1f));
            Assert.That(reloaded.LocalZ, Is.EqualTo(-7.25f));
            Assert.That(reloaded.HeadingY, Is.EqualTo(90f));
        }

        [Test]
        public void RegionLastSeenTickRoundTripsAndOverwrites()
        {
            GenerateStarterWorld();
            RegionState region = LoadFirstRegion();
            var repository = new LiveRegionRepository(pathProvider);

            Assert.That(repository.GetRegionLastSeenTick(region.Id), Is.Null);

            repository.UpsertRegionLastSeenTick(region.Id, new GameTick(7));
            Assert.That(repository.GetRegionLastSeenTick(region.Id).Value.Value, Is.EqualTo(7));

            repository.UpsertRegionLastSeenTick(region.Id, new GameTick(11));
            Assert.That(repository.GetRegionLastSeenTick(region.Id).Value.Value, Is.EqualTo(11));
        }

        private RegionState LoadFirstRegion()
        {
            return new StarterWorldReadRepository(pathProvider).LoadStarterWorld().Regions.First();
        }

        private EntityState LoadFirstEntity()
        {
            return new StarterWorldReadRepository(pathProvider).LoadStarterWorld().Entities.First();
        }

        private void GenerateStarterWorld()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(LiveRegionTestWorld.CreateValidBuildResult());
        }

        private sealed class AuthorityRow
        {
            public string Mode { get; set; }
            public string Owner { get; set; }
        }
    }
}
