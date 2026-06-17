using System;
using System.Diagnostics;
using FantasySim.Content.Catalog;
using FantasySim.Persistence.SQLite;
using FantasySim.Simulation;
using FantasySim.Simulation.State;

namespace FantasySim.Session
{
    public sealed class RegionSimulationService : IPartitionTicker
    {
        private readonly StarterWorldReadRepository readRepository;
        private readonly RegionStateWriteRepository writeRepository;
        private readonly StarterWorldSimulationPipeline pipeline;

        public RegionSimulationService(DatabasePathProvider pathProvider = null)
            : this(
                new StarterWorldReadRepository(pathProvider),
                new RegionStateWriteRepository(pathProvider),
                BuildDefaultPipeline()
            )
        {
        }

        public RegionSimulationService(
            StarterWorldReadRepository readRepository,
            RegionStateWriteRepository writeRepository,
            StarterWorldSimulationPipeline pipeline
        )
        {
            this.readRepository = readRepository ?? throw new ArgumentNullException(nameof(readRepository));
            this.writeRepository = writeRepository ?? throw new ArgumentNullException(nameof(writeRepository));
            this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public PartitionTickReport TickPartition(long partitionId)
        {
            var stopwatch = Stopwatch.StartNew();
            LoadedPartitionState state = readRepository.LoadPartition(partitionId);
            PartitionTickDelta delta = pipeline.Tick(state);
            RegionStateWriteResult result = writeRepository.WritePartitionTick(state, delta);
            stopwatch.Stop();

            return new PartitionTickReport(
                partitionId,
                result.PreviousTick,
                result.CurrentTick,
                result.EventsInserted,
                delta.RegionDeltas.Count,
                stopwatch.Elapsed.TotalMilliseconds
            );
        }

        public static StarterWorldSimulationPipeline BuildDefaultPipeline()
        {
            ContentCatalogBuildResult result = new ContentCatalogBuilder().Build();
            return BuildPipeline(result.Catalog);
        }

        public static StarterWorldSimulationPipeline BuildPipeline(ContentCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var progressions = GoalProgressionParser.Parse(catalog.Goals.Values);
            var goalAdvancementSystem = new GoalAdvancementSystem(progressions);
            return new StarterWorldSimulationPipeline(goalAdvancementSystem: goalAdvancementSystem);
        }
    }
}
