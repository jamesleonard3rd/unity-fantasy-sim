using System;
using System.Collections.Generic;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using SQLite;
using UnityEngine;

namespace FantasySim.Persistence.SQLite
{
    public sealed class StarterWorldGenerator
    {
        private const int TicksPerGameDay = 24;
        private const int DaysPerYear = 365;
        private const string StarterPartitionKey = "starter_world";

        public StarterWorldGenerationResult GenerateIfEmpty(SQLiteConnection connection, ContentCatalog catalog)
        {
            if (HasStarterWorldRows(connection))
            {
                return new StarterWorldGenerationResult(
                    false,
                    "Starter world generation skipped because save-state rows already exist.",
                    0,
                    0,
                    0
                );
            }

            int regionsInserted = 0;
            int entitiesInserted = 0;

            connection.RunInTransaction(() =>
            {
                connection.Execute(
                    @"INSERT INTO world_clock (id, game_tick, ticks_per_game_day, updated_utc)
                      VALUES (1, 0, ?, ?)",
                    TicksPerGameDay,
                    DateTime.UtcNow.ToString("O")
                );

                var regionIdsByKey = new Dictionary<string, long>();
                foreach (RegionDefinition region in catalog.Regions.Values)
                {
                    connection.Execute(
                        @"INSERT INTO regions (key, display_name, type, parent_id, tick_interval_seconds)
                          VALUES (?, ?, ?, NULL, ?)",
                        region.key,
                        region.displayName,
                        string.IsNullOrWhiteSpace(region.type) ? "region" : region.type,
                        region.tickIntervalSeconds
                    );
                    regionIdsByKey.Add(region.key, connection.ExecuteScalar<long>("SELECT last_insert_rowid()"));
                    regionsInserted++;
                }

                foreach (RegionDefinition region in catalog.Regions.Values)
                {
                    if (string.IsNullOrWhiteSpace(region.parentKey))
                    {
                        continue;
                    }

                    connection.Execute(
                        "UPDATE regions SET parent_id = ? WHERE id = ?",
                        regionIdsByKey[region.parentKey],
                        regionIdsByKey[region.key]
                    );
                }

                connection.Execute(
                    @"INSERT INTO simulation_partitions (key, display_name, tick_interval_seconds)
                      VALUES (?, ?, ?)",
                    StarterPartitionKey,
                    "Starter World",
                    180f
                );
                long partitionId = connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

                foreach (long regionId in regionIdsByKey.Values)
                {
                    connection.Execute(
                        @"INSERT INTO simulation_partition_regions (partition_id, region_id)
                          VALUES (?, ?)",
                        partitionId,
                        regionId
                    );
                }

                foreach (EntityTemplateDefinition entityTemplate in catalog.EntityTemplates.Values)
                {
                    connection.Execute(
                        @"INSERT INTO entities (
                            template_key,
                            display_name,
                            race_key,
                            subrace_key,
                            age_ticks,
                            payload_json
                          )
                          VALUES (?, ?, ?, ?, ?, ?)",
                        entityTemplate.key,
                        entityTemplate.displayName,
                        entityTemplate.raceKey,
                        entityTemplate.subraceKey,
                        ConvertYearsToTicks(entityTemplate.age),
                        JsonUtility.ToJson(EntityTemplatePayload.From(entityTemplate))
                    );
                    long entityId = connection.ExecuteScalar<long>("SELECT last_insert_rowid()");
                    entitiesInserted++;

                    connection.Execute(
                        @"INSERT INTO entity_positions (entity_id, region_id)
                          VALUES (?, ?)",
                        entityId,
                        regionIdsByKey[entityTemplate.regionKey]
                    );
                }
            });

            return new StarterWorldGenerationResult(
                true,
                "Starter world generated.",
                regionsInserted,
                1,
                entitiesInserted
            );
        }

        private static bool HasStarterWorldRows(SQLiteConnection connection)
        {
            int clockRows = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM world_clock");
            int regionRows = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM regions");
            int entityRows = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM entities");
            return clockRows > 0 || regionRows > 0 || entityRows > 0;
        }

        private static long ConvertYearsToTicks(int years)
        {
            return Math.Max(0, years) * DaysPerYear * TicksPerGameDay;
        }

        [Serializable]
        private sealed class EntityTemplatePayload
        {
            public string gender;
            public string houseKey;
            public string role;
            public string[] traitKeys;
            public string[] magicKeys;
            public EntityFactionMembershipDefinition[] factions;
            public string notes;

            public static EntityTemplatePayload From(EntityTemplateDefinition entityTemplate)
            {
                return new EntityTemplatePayload
                {
                    gender = entityTemplate.gender,
                    houseKey = entityTemplate.houseKey,
                    role = entityTemplate.role,
                    traitKeys = entityTemplate.traitKeys ?? Array.Empty<string>(),
                    magicKeys = entityTemplate.magicKeys ?? Array.Empty<string>(),
                    factions = entityTemplate.factions ?? Array.Empty<EntityFactionMembershipDefinition>(),
                    notes = entityTemplate.notes
                };
            }
        }
    }
}
