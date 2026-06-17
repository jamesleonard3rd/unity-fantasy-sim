using FantasySim.Persistence.SQLite;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using UnityEditor;
using UnityEngine;

namespace FantasySim.Editor.SimulationTools
{
    public static class SimulationMenuItems
    {
        [MenuItem("Fantasy Sim/Simulation/Tick Starter World")]
        public static void TickStarterWorld()
        {
            DatabasePathProvider pathProvider = DatabasePathProvider.ForEditorDevelopmentDatabase();
            var readRepository = new StarterWorldReadRepository(pathProvider);
            var writeRepository = new RegionStateWriteRepository(pathProvider);
            var simulationPipeline = new StarterWorldSimulationPipeline();

            LoadedPartitionState state = readRepository.LoadStarterWorld();
            PartitionTickDelta delta = simulationPipeline.Tick(state);
            RegionStateWriteResult result = writeRepository.WritePartitionTick(state, delta);

            Debug.Log(
                "Ticked Fantasy Sim starter world.\n" +
                $"Database: {pathProvider.DatabasePath}\n" +
                $"Previous tick: {result.PreviousTick.Value}\n" +
                $"Current tick: {result.CurrentTick.Value}\n" +
                $"Events inserted: {result.EventsInserted}"
            );
        }
    }
}
