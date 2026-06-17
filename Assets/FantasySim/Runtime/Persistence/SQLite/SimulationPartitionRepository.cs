using System.Collections.Generic;
using System.Linq;
using SQLite;

namespace FantasySim.Persistence.SQLite
{
    public sealed class SimulationPartitionRepository
    {
        public const string LiveAuthorityMode = "live";

        private readonly SqliteConnectionFactory connectionFactory;

        public SimulationPartitionRepository(DatabasePathProvider pathProvider = null)
        {
            connectionFactory = new SqliteConnectionFactory(
                pathProvider ?? DatabasePathProvider.ForDefaultGameDatabase()
            );
        }

        public IReadOnlyList<CoarsePartitionInfo> LoadCoarsePartitions()
        {
            using SQLiteConnection connection = connectionFactory.OpenConnection();

            List<PartitionRow> rows = connection.Query<PartitionRow>(
                @"SELECT p.id AS Id,
                         p.key AS Key,
                         p.tick_interval_seconds AS TickIntervalSeconds,
                         p.last_tick_game_tick AS LastTickGameTick
                  FROM simulation_partitions p
                  WHERE p.authority_mode != ?
                    AND NOT EXISTS (
                      SELECT 1
                      FROM simulation_partition_regions spr
                      INNER JOIN regions r ON r.id = spr.region_id
                      WHERE spr.partition_id = p.id
                        AND r.authority_mode = ?
                    )
                  ORDER BY p.id",
                LiveAuthorityMode,
                LiveAuthorityMode
            );

            return rows
                .Select(row => new CoarsePartitionInfo(
                    row.Id,
                    row.Key,
                    row.TickIntervalSeconds,
                    row.LastTickGameTick
                ))
                .ToList();
        }

        public sealed class PartitionRow
        {
            public long Id { get; set; }
            public string Key { get; set; }
            public float TickIntervalSeconds { get; set; }
            public long LastTickGameTick { get; set; }
        }
    }

    public sealed class CoarsePartitionInfo
    {
        public CoarsePartitionInfo(long id, string key, float tickIntervalSeconds, long lastTickGameTick)
        {
            Id = id;
            Key = key;
            TickIntervalSeconds = tickIntervalSeconds;
            LastTickGameTick = lastTickGameTick;
        }

        public long Id { get; }
        public string Key { get; }
        public float TickIntervalSeconds { get; }
        public long LastTickGameTick { get; }
    }
}
