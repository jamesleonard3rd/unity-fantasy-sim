using System;
using System.Collections.Generic;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using SQLite;

namespace FantasySim.Persistence.SQLite
{
    public sealed class LiveRegionRepository
    {
        public const string LiveAuthorityMode = SimulationPartitionRepository.LiveAuthorityMode;
        public const string CoarseAuthorityMode = "coarse";

        private readonly SqliteConnectionFactory connectionFactory;

        public LiveRegionRepository(DatabasePathProvider pathProvider = null)
        {
            connectionFactory = new SqliteConnectionFactory(
                pathProvider ?? DatabasePathProvider.ForDefaultGameDatabase()
            );
        }

        public void SetRegionAuthority(RegionId regionId, string mode, string owner = null, GameTick? untilTick = null)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                throw new ArgumentException("Authority mode is required.", nameof(mode));
            }

            using SQLiteConnection connection = connectionFactory.OpenConnection();
            int rowsUpdated = connection.Execute(
                @"UPDATE regions
                  SET authority_mode = ?, authority_owner = ?, authority_until_tick = ?
                  WHERE id = ?",
                mode,
                string.IsNullOrWhiteSpace(owner) ? null : owner,
                untilTick.HasValue ? (object)untilTick.Value.Value : null,
                regionId.Value
            );

            if (rowsUpdated != 1)
            {
                throw new InvalidOperationException($"Cannot set authority because region {regionId.Value} is missing.");
            }
        }

        public int WriteEntityPositions(IReadOnlyList<EntityPositionUpdate> positions)
        {
            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }

            if (positions.Count == 0)
            {
                return 0;
            }

            int rowsUpdated = 0;
            using SQLiteConnection connection = connectionFactory.OpenConnection();
            connection.RunInTransaction(() =>
            {
                foreach (EntityPositionUpdate position in positions)
                {
                    rowsUpdated += connection.Execute(
                        @"UPDATE entity_positions
                          SET region_id = ?, local_x = ?, local_y = ?, local_z = ?, heading_y = ?
                          WHERE entity_id = ?",
                        position.RegionId.Value,
                        position.LocalX,
                        position.LocalY,
                        position.LocalZ,
                        position.HeadingY,
                        position.EntityId.Value
                    );
                }
            });

            return rowsUpdated;
        }

        public GameTick? GetRegionLastSeenTick(RegionId regionId)
        {
            using SQLiteConnection connection = connectionFactory.OpenConnection();
            List<LastSeenRow> rows = connection.Query<LastSeenRow>(
                "SELECT last_seen_game_tick AS LastSeenGameTick FROM region_visits WHERE region_id = ?",
                regionId.Value
            );

            if (rows.Count == 0)
            {
                return null;
            }

            return new GameTick(rows[0].LastSeenGameTick);
        }

        public void UpsertRegionLastSeenTick(RegionId regionId, GameTick lastSeenTick)
        {
            using SQLiteConnection connection = connectionFactory.OpenConnection();
            connection.Execute(
                @"INSERT OR REPLACE INTO region_visits (region_id, last_seen_game_tick)
                  VALUES (?, ?)",
                regionId.Value,
                lastSeenTick.Value
            );
        }

        public sealed class LastSeenRow
        {
            public long LastSeenGameTick { get; set; }
        }
    }

    public sealed class EntityPositionUpdate
    {
        public EntityPositionUpdate(
            EntityId entityId,
            RegionId regionId,
            float? localX,
            float? localY,
            float? localZ,
            float? headingY
        )
        {
            EntityId = entityId;
            RegionId = regionId;
            LocalX = localX;
            LocalY = localY;
            LocalZ = localZ;
            HeadingY = headingY;
        }

        public EntityId EntityId { get; }
        public RegionId RegionId { get; }
        public float? LocalX { get; }
        public float? LocalY { get; }
        public float? LocalZ { get; }
        public float? HeadingY { get; }
    }
}
