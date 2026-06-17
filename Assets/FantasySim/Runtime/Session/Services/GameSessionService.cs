using System;
using FantasySim.Persistence.SQLite;

namespace FantasySim.Session
{
    public sealed class GameSessionService
    {
        private readonly RegionSimulationService simulationService;
        private readonly SimulationPartitionRepository partitionRepository;
        private readonly CoarseRegionScheduler scheduler;

        public GameSessionService(ITimeSource timeSource, DatabasePathProvider pathProvider = null)
            : this(
                new RegionSimulationService(pathProvider),
                new SimulationPartitionRepository(pathProvider),
                timeSource
            )
        {
        }

        public GameSessionService(
            RegionSimulationService simulationService,
            SimulationPartitionRepository partitionRepository,
            ITimeSource timeSource
        )
        {
            this.simulationService = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
            this.partitionRepository = partitionRepository ?? throw new ArgumentNullException(nameof(partitionRepository));
            if (timeSource == null)
            {
                throw new ArgumentNullException(nameof(timeSource));
            }

            scheduler = new CoarseRegionScheduler(
                timeSource,
                () => this.partitionRepository.LoadCoarsePartitions(),
                this.simulationService
            );
        }

        public bool IsRunning { get; private set; }

        public CoarseRegionScheduler Scheduler => scheduler;

        public void StartSession()
        {
            scheduler.Initialize();
            IsRunning = true;
        }

        public PartitionTickReport Update()
        {
            return IsRunning ? scheduler.Update() : null;
        }

        public void StopSession()
        {
            IsRunning = false;
        }
    }
}
