using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasySim.Core.Time;
using FantasySim.Persistence.SQLite;
using FantasySim.Session;
using FantasySim.Simulation.State;
using NUnit.Framework;

namespace FantasySim.Tests.EditMode.Session
{
    public sealed class LiveRegionAuthorityServiceTests
    {
        private string tempDirectory;
        private string databasePath;
        private DatabasePathProvider pathProvider;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), $"FantasySimLiveRegionServiceTests_{Guid.NewGuid():N}");
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
        public void EnterLiveRegionMarksRegionLiveAndExcludesPartitionFromScheduler()
        {
            GenerateStarterWorld();
            LiveRegionAuthorityService service = CreateService();

            LiveRegionEntry entry = service.EnterLiveRegion(
                LiveRegionAuthorityService.DefaultPartitionKey,
                LiveRegionTestWorld.ChildRegionKey
            );

            Assert.That(entry.Region.Key, Is.EqualTo(LiveRegionTestWorld.ChildRegionKey));
            Assert.That(entry.Entities, Is.Not.Empty);

            var partitionRepository = new SimulationPartitionRepository(pathProvider);
            Assert.That(partitionRepository.LoadCoarsePartitions(), Is.Empty);
        }

        [Test]
        public void ExitLiveRegionWritesPositionsRestoresCoarseAndRecordsLastSeen()
        {
            GenerateStarterWorld();
            LiveRegionAuthorityService service = CreateService();

            LiveRegionEntry entry = service.EnterLiveRegion(
                LiveRegionAuthorityService.DefaultPartitionKey,
                LiveRegionTestWorld.ChildRegionKey
            );
            EntityState entity = entry.Entities.First();

            var positions = new List<EntityPositionUpdate>
            {
                new EntityPositionUpdate(entity.Id, entity.RegionId, 4f, 0f, 8f, 45f)
            };

            int written = service.ExitLiveRegion(entry.Region.Id, positions, new GameTick(5));
            Assert.That(written, Is.EqualTo(1));

            var partitionRepository = new SimulationPartitionRepository(pathProvider);
            Assert.That(partitionRepository.LoadCoarsePartitions(), Is.Not.Empty);

            LoadedPartitionState reloaded = new StarterWorldReadRepository(pathProvider).LoadStarterWorld();
            EntityState reloadedEntity = reloaded.Entities.Single(candidate => candidate.Id.Value == entity.Id.Value);
            Assert.That(reloadedEntity.LocalX, Is.EqualTo(4f));
            Assert.That(reloadedEntity.LocalZ, Is.EqualTo(8f));
            Assert.That(reloadedEntity.HeadingY, Is.EqualTo(45f));

            GameTick? lastSeen = new LiveRegionRepository(pathProvider).GetRegionLastSeenTick(entry.Region.Id);
            Assert.That(lastSeen.HasValue, Is.True);
            Assert.That(lastSeen.Value.Value, Is.EqualTo(5));
        }

        private LiveRegionAuthorityService CreateService()
        {
            return new LiveRegionAuthorityService(pathProvider);
        }

        private void GenerateStarterWorld()
        {
            var initializer = new DatabaseInitializer(pathProvider);
            initializer.GenerateStarterWorldIfEmpty(LiveRegionTestWorld.CreateValidBuildResult());
        }
    }
}
