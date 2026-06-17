using System.Collections.Generic;
using System.Linq;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using SQLite;

namespace FantasySim.Persistence.SQLite
{
    public sealed class WorldEventReadRepository
    {
        public const int DefaultLimit = 200;

        private readonly SqliteConnectionFactory connectionFactory;

        public WorldEventReadRepository(DatabasePathProvider pathProvider = null)
        {
            connectionFactory = new SqliteConnectionFactory(
                pathProvider ?? DatabasePathProvider.ForDefaultGameDatabase()
            );
        }

        public IReadOnlyList<WorldEventRecord> Query(WorldEventQuery query = null)
        {
            query ??= new WorldEventQuery();

            var conditions = new List<string>();
            var parameters = new List<object>();

            if (query.RegionId.HasValue)
            {
                conditions.Add("region_id = ?");
                parameters.Add(query.RegionId.Value.Value);
            }

            if (query.EntityId.HasValue)
            {
                conditions.Add("(subject_entity_id = ? OR target_entity_id = ?)");
                parameters.Add(query.EntityId.Value.Value);
                parameters.Add(query.EntityId.Value.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                conditions.Add("type = ?");
                parameters.Add(query.Type);
            }

            if (query.MinSignificance.HasValue)
            {
                conditions.Add("significance >= ?");
                parameters.Add(query.MinSignificance.Value);
            }

            if (query.MinTick.HasValue)
            {
                conditions.Add("occurred_game_tick >= ?");
                parameters.Add(query.MinTick.Value.Value);
            }

            if (query.MaxTick.HasValue)
            {
                conditions.Add("occurred_game_tick <= ?");
                parameters.Add(query.MaxTick.Value.Value);
            }

            string whereClause = conditions.Count > 0
                ? "WHERE " + string.Join(" AND ", conditions)
                : string.Empty;

            int limit = query.Limit > 0 ? query.Limit : DefaultLimit;
            parameters.Add(limit);

            using SQLiteConnection connection = connectionFactory.OpenConnection();
            List<EventRow> rows = connection.Query<EventRow>(
                $@"SELECT id AS Id,
                          region_id AS RegionId,
                          type AS Type,
                          subject_entity_id AS SubjectEntityId,
                          target_entity_id AS TargetEntityId,
                          significance AS Significance,
                          payload_json AS PayloadJson,
                          occurred_game_tick AS OccurredGameTick
                   FROM world_events
                   {whereClause}
                   ORDER BY occurred_game_tick DESC, id DESC
                   LIMIT ?",
                parameters.ToArray()
            );

            return rows.Select(row => new WorldEventRecord(
                row.Id,
                row.RegionId.HasValue ? new RegionId(row.RegionId.Value) : (RegionId?)null,
                row.Type,
                row.SubjectEntityId.HasValue ? new EntityId(row.SubjectEntityId.Value) : (EntityId?)null,
                row.TargetEntityId.HasValue ? new EntityId(row.TargetEntityId.Value) : (EntityId?)null,
                row.Significance,
                row.PayloadJson,
                new GameTick(row.OccurredGameTick)
            )).ToList();
        }

        public sealed class EventRow
        {
            public long Id { get; set; }
            public long? RegionId { get; set; }
            public string Type { get; set; }
            public long? SubjectEntityId { get; set; }
            public long? TargetEntityId { get; set; }
            public int Significance { get; set; }
            public string PayloadJson { get; set; }
            public long OccurredGameTick { get; set; }
        }
    }

    public sealed class WorldEventQuery
    {
        public RegionId? RegionId { get; set; }
        public EntityId? EntityId { get; set; }
        public string Type { get; set; }
        public int? MinSignificance { get; set; }
        public GameTick? MinTick { get; set; }
        public GameTick? MaxTick { get; set; }
        public int Limit { get; set; } = WorldEventReadRepository.DefaultLimit;
    }

    public sealed class WorldEventRecord
    {
        public WorldEventRecord(
            long id,
            RegionId? regionId,
            string type,
            EntityId? subjectEntityId,
            EntityId? targetEntityId,
            int significance,
            string payloadJson,
            GameTick occurredTick
        )
        {
            Id = id;
            RegionId = regionId;
            Type = type;
            SubjectEntityId = subjectEntityId;
            TargetEntityId = targetEntityId;
            Significance = significance;
            PayloadJson = payloadJson;
            OccurredTick = occurredTick;
        }

        public long Id { get; }
        public RegionId? RegionId { get; }
        public string Type { get; }
        public EntityId? SubjectEntityId { get; }
        public EntityId? TargetEntityId { get; }
        public int Significance { get; }
        public string PayloadJson { get; }
        public GameTick OccurredTick { get; }
    }
}
