using System.Collections.Generic;
using System.Linq;
using FantasySim.Persistence.SQLite;
using FantasySim.Session;
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
            var partitionRepository = new SimulationPartitionRepository(pathProvider);
            CoarsePartitionInfo starter = partitionRepository
                .LoadCoarsePartitions()
                .FirstOrDefault(partition => partition.Key == StarterWorldReadRepository.StarterPartitionKey);

            if (starter == null)
            {
                Debug.LogWarning(
                    "Cannot tick the starter world because no coarse 'starter_world' partition exists. " +
                    "Generate the starter world first."
                );
                return;
            }

            var simulationService = new RegionSimulationService(pathProvider);
            PartitionTickReport report = simulationService.TickPartition(starter.Id);

            Debug.Log(
                "Ticked Fantasy Sim starter world.\n" +
                $"Database: {pathProvider.DatabasePath}\n" +
                $"Partition: {starter.Id} ({starter.Key})\n" +
                $"Previous tick: {report.PreviousTick.Value}\n" +
                $"Current tick: {report.CurrentTick.Value}\n" +
                $"Events inserted: {report.EventsInserted}\n" +
                $"Elapsed: {report.ElapsedMilliseconds:F1} ms"
            );
        }

        [MenuItem("Fantasy Sim/Simulation/Tick Due Partitions")]
        public static void TickDuePartitions()
        {
            DatabasePathProvider pathProvider = DatabasePathProvider.ForEditorDevelopmentDatabase();
            var session = new GameSessionService(new EditorTimeSource(), pathProvider);
            session.StartSession();
            IReadOnlyList<PartitionTickReport> reports = session.Scheduler.RunDuePartitions();
            session.StopSession();

            if (reports.Count == 0)
            {
                Debug.Log(
                    $"No coarse partitions were due to tick.\nDatabase: {pathProvider.DatabasePath}"
                );
                return;
            }

            int totalEvents = reports.Sum(report => report.EventsInserted);
            string partitionSummary = string.Join(
                ", ",
                reports.Select(report => $"{report.PartitionId} (tick {report.CurrentTick.Value})")
            );

            Debug.Log(
                $"Ticked {reports.Count} coarse partition(s).\n" +
                $"Database: {pathProvider.DatabasePath}\n" +
                $"Partitions: {partitionSummary}\n" +
                $"Total events inserted: {totalEvents}"
            );
        }

        private sealed class EditorTimeSource : ITimeSource
        {
            public double NowSeconds => 0.0;
        }
    }
}
