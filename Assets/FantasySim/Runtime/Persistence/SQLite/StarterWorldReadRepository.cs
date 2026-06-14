using System;
using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Simulation.State;
using SQLite;

namespace FantasySim.Persistence.SQLite
{
    public sealed class StarterWorldReadRepository
    {
        public const string StarterPartitionKey = "starter_world";

        private readonly SqliteConnectionFactory connectionFactory;

        public StarterWorldReadRepository(DatabasePathProvider pathProvider = null)
        {
            connectionFactory = new SqliteConnectionFactory(
                pathProvider ?? DatabasePathProvider.ForDefaultGameDatabase()
            );
        }

        public LoadedPartitionState LoadStarterWorld()
        {
            return LoadPartition(StarterPartitionKey);
        }

        public LoadedPartitionState LoadPartition(string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
            {
                throw new ArgumentException("Partition key is required.", nameof(partitionKey));
            }

            using SQLiteConnection connection = connectionFactory.OpenConnection();

            ClockRow clock = LoadClock(connection);
            PartitionRow partition = LoadPartitionRow(connection, partitionKey);
            List<RegionState> regions = LoadRegions(connection, partition.Id);
            EnsureEntitiesHavePositions(connection);
            List<EntityState> entities = LoadEntities(connection, regions);

            var tickContext = new TickContext(
                new GameTick(clock.GameTick),
                clock.TicksPerGameDay,
                partition.Id,
                partition.Key,
                partition.DisplayName,
                partition.TickIntervalSeconds,
                new GameTick(partition.LastTickGameTick)
            );

            return new LoadedPartitionState(tickContext, regions, entities);
        }

        private static ClockRow LoadClock(SQLiteConnection connection)
        {
            ClockRow clock = connection.Query<ClockRow>(
                @"SELECT game_tick AS GameTick,
                         ticks_per_game_day AS TicksPerGameDay
                  FROM world_clock
                  WHERE id = 1"
            ).FirstOrDefault();

            if (clock == null)
            {
                throw new InvalidOperationException("Cannot load starter world because world_clock is missing.");
            }

            return clock;
        }

        private static PartitionRow LoadPartitionRow(SQLiteConnection connection, string partitionKey)
        {
            PartitionRow partition = connection.Query<PartitionRow>(
                @"SELECT id AS Id,
                         key AS Key,
                         display_name AS DisplayName,
                         tick_interval_seconds AS TickIntervalSeconds,
                         last_tick_game_tick AS LastTickGameTick
                  FROM simulation_partitions
                  WHERE key = ?",
                partitionKey
            ).FirstOrDefault();

            if (partition == null)
            {
                throw new InvalidOperationException($"Cannot load partition '{partitionKey}' because it is missing.");
            }

            return partition;
        }

        private static List<RegionState> LoadRegions(SQLiteConnection connection, long partitionId)
        {
            List<RegionRow> rows = connection.Query<RegionRow>(
                @"SELECT r.id AS Id,
                         r.key AS Key,
                         r.display_name AS DisplayName,
                         r.type AS Type,
                         r.parent_id AS ParentId,
                         r.tick_interval_seconds AS TickIntervalSeconds,
                         r.last_tick_game_tick AS LastTickGameTick,
                         r.authority_mode AS AuthorityMode,
                         r.authority_owner AS AuthorityOwner,
                         r.authority_until_tick AS AuthorityUntilTick
                  FROM regions r
                  INNER JOIN simulation_partition_regions spr ON spr.region_id = r.id
                  WHERE spr.partition_id = ?
                  ORDER BY r.id",
                partitionId
            );

            return rows.Select(row => new RegionState(
                new RegionId(row.Id),
                row.Key,
                row.DisplayName,
                row.Type,
                row.ParentId.HasValue ? new RegionId(row.ParentId.Value) : (RegionId?)null,
                row.TickIntervalSeconds,
                new GameTick(row.LastTickGameTick),
                row.AuthorityMode,
                row.AuthorityOwner,
                row.AuthorityUntilTick.HasValue
                    ? new GameTick(row.AuthorityUntilTick.Value)
                    : (GameTick?)null
            )).ToList();
        }

        private static void EnsureEntitiesHavePositions(SQLiteConnection connection)
        {
            MissingEntityPositionRow row = connection.Query<MissingEntityPositionRow>(
                @"SELECT e.id
                  FROM entities e
                  LEFT JOIN entity_positions p ON p.entity_id = e.id
                  WHERE p.entity_id IS NULL
                  LIMIT 1"
            ).FirstOrDefault();

            if (row != null)
            {
                throw new InvalidOperationException(
                    $"Cannot load starter world because entity {row.Id} is missing a position."
                );
            }
        }

        private static List<EntityState> LoadEntities(SQLiteConnection connection, IReadOnlyList<RegionState> regions)
        {
            if (regions.Count == 0)
            {
                return new List<EntityState>();
            }

            string regionPlaceholders = string.Join(", ", regions.Select(_ => "?"));
            object[] regionIds = regions.Select(region => (object)region.Id.Value).ToArray();

            List<EntityRow> rows = connection.Query<EntityRow>(
                $@"SELECT e.id AS Id,
                          e.template_key AS TemplateKey,
                          e.display_name AS DisplayName,
                          e.race_key AS RaceKey,
                          e.subrace_key AS SubraceKey,
                          e.age_ticks AS AgeTicks,
                          e.is_alive AS IsAlive,
                          e.payload_json AS PayloadJson,
                          p.region_id AS RegionId,
                          p.local_x AS LocalX,
                          p.local_y AS LocalY,
                          p.local_z AS LocalZ,
                          p.heading_y AS HeadingY,
                          n.entity_id AS NeedsEntityId,
                          n.hunger AS Hunger,
                          n.fatigue AS Fatigue,
                          n.mood AS Mood,
                          n.stress AS Stress
                   FROM entities e
                   INNER JOIN entity_positions p ON p.entity_id = e.id
                   LEFT JOIN entity_needs n ON n.entity_id = e.id
                   WHERE p.region_id IN ({regionPlaceholders})
                   ORDER BY e.id",
                regionIds
            );

            var entities = new List<EntityState>(rows.Count);
            foreach (EntityRow row in rows)
            {
                if (!row.NeedsEntityId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Cannot load starter world because entity {row.Id} is missing needs."
                    );
                }

                var entityId = new EntityId(row.Id);
                entities.Add(new EntityState(
                    entityId,
                    row.TemplateKey,
                    row.DisplayName,
                    row.RaceKey,
                    row.SubraceKey,
                    row.AgeTicks,
                    row.IsAlive != 0,
                    row.PayloadJson,
                    new RegionId(row.RegionId),
                    row.LocalX,
                    row.LocalY,
                    row.LocalZ,
                    row.HeadingY,
                    new EntityNeedsState(
                        entityId,
                        row.Hunger.GetValueOrDefault(),
                        row.Fatigue.GetValueOrDefault(),
                        row.Mood.GetValueOrDefault(),
                        row.Stress.GetValueOrDefault()
                    )
                ));
            }

            return entities;
        }

        public sealed class ClockRow
        {
            public long GameTick { get; set; }
            public int TicksPerGameDay { get; set; }
        }

        public sealed class PartitionRow
        {
            public long Id { get; set; }
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public float TickIntervalSeconds { get; set; }
            public long LastTickGameTick { get; set; }
        }

        public sealed class RegionRow
        {
            public long Id { get; set; }
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public string Type { get; set; }
            public long? ParentId { get; set; }
            public float TickIntervalSeconds { get; set; }
            public long LastTickGameTick { get; set; }
            public string AuthorityMode { get; set; }
            public string AuthorityOwner { get; set; }
            public long? AuthorityUntilTick { get; set; }
        }

        public sealed class EntityRow
        {
            public long Id { get; set; }
            public string TemplateKey { get; set; }
            public string DisplayName { get; set; }
            public string RaceKey { get; set; }
            public string SubraceKey { get; set; }
            public long AgeTicks { get; set; }
            public int IsAlive { get; set; }
            public string PayloadJson { get; set; }
            public long RegionId { get; set; }
            public float? LocalX { get; set; }
            public float? LocalY { get; set; }
            public float? LocalZ { get; set; }
            public float? HeadingY { get; set; }
            public long? NeedsEntityId { get; set; }
            public float? Hunger { get; set; }
            public float? Fatigue { get; set; }
            public float? Mood { get; set; }
            public float? Stress { get; set; }
        }

        public sealed class MissingEntityPositionRow
        {
            public long Id { get; set; }
        }
    }
}
