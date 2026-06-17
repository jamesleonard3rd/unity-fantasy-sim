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
        private const string KinshipTermType = "kinship";
        private const string HouseTermType = "house";
        private const int HouseAffinityValue = 15;
        private const string DefaultGoalState = "inactive";

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
            int relationshipsInserted = 0;
            int relationshipTermsInserted = 0;
            int entityGoalsInserted = 0;
            var entityIdByTemplateKey = new Dictionary<string, long>();

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
                    entityIdByTemplateKey[entityTemplate.key] = entityId;

                    connection.Execute(
                        @"INSERT INTO entity_positions (entity_id, region_id)
                          VALUES (?, ?)",
                        entityId,
                        regionIdsByKey[entityTemplate.regionKey]
                    );
                }

                SocialSeedCounts socialCounts = SeedRelationshipsAndTerms(connection, catalog, entityIdByTemplateKey);
                relationshipsInserted = socialCounts.RelationshipsInserted;
                relationshipTermsInserted = socialCounts.RelationshipTermsInserted;
                entityGoalsInserted = SeedDefaultGoals(connection, catalog, entityIdByTemplateKey);
            });

            return new StarterWorldGenerationResult(
                true,
                "Starter world generated.",
                regionsInserted,
                1,
                entitiesInserted,
                relationshipsInserted,
                relationshipTermsInserted,
                entityGoalsInserted
            );
        }

        private static SocialSeedCounts SeedRelationshipsAndTerms(
            SQLiteConnection connection,
            ContentCatalog catalog,
            IReadOnlyDictionary<string, long> entityIdByTemplateKey
        )
        {
            var opinionBySubjectTarget = new Dictionary<long, Dictionary<long, int>>();
            int termsInserted = 0;

            foreach (EntityRelationshipDefinition relationship in catalog.EntityRelationships)
            {
                if (!entityIdByTemplateKey.TryGetValue(relationship.fromKey ?? string.Empty, out long subjectId))
                {
                    continue;
                }

                if (!entityIdByTemplateKey.TryGetValue(relationship.toKey ?? string.Empty, out long targetId))
                {
                    continue;
                }

                InsertRelationshipTerm(connection, subjectId, targetId, KinshipTermType, relationship.opinion, KinshipPayload(relationship.type));
                AccumulateOpinion(opinionBySubjectTarget, subjectId, targetId, relationship.opinion);
                termsInserted++;
            }

            foreach (KeyValuePair<string, List<long>> house in GroupEntitiesByHouse(catalog, entityIdByTemplateKey))
            {
                List<long> members = house.Value;
                for (int subjectIndex = 0; subjectIndex < members.Count; subjectIndex++)
                {
                    for (int targetIndex = 0; targetIndex < members.Count; targetIndex++)
                    {
                        if (subjectIndex == targetIndex)
                        {
                            continue;
                        }

                        InsertRelationshipTerm(connection, members[subjectIndex], members[targetIndex], HouseTermType, HouseAffinityValue, HousePayload(house.Key));
                        AccumulateOpinion(opinionBySubjectTarget, members[subjectIndex], members[targetIndex], HouseAffinityValue);
                        termsInserted++;
                    }
                }
            }

            int relationshipsInserted = 0;
            foreach (KeyValuePair<long, Dictionary<long, int>> subjectEntry in Ordered(opinionBySubjectTarget))
            {
                foreach (KeyValuePair<long, int> targetEntry in Ordered(subjectEntry.Value))
                {
                    InsertRelationship(connection, subjectEntry.Key, targetEntry.Key, ClampOpinion(targetEntry.Value));
                    relationshipsInserted++;
                }
            }

            return new SocialSeedCounts(relationshipsInserted, termsInserted);
        }

        private static int SeedDefaultGoals(
            SQLiteConnection connection,
            ContentCatalog catalog,
            IReadOnlyDictionary<string, long> entityIdByTemplateKey
        )
        {
            var goalKeys = new List<string>(catalog.Goals.Keys);
            goalKeys.Sort(StringComparer.Ordinal);

            int inserted = 0;
            var entityIds = new List<long>(entityIdByTemplateKey.Values);
            entityIds.Sort();

            foreach (long entityId in entityIds)
            {
                foreach (string goalKey in goalKeys)
                {
                    connection.Execute(
                        @"INSERT INTO entity_goals (entity_id, goal_key, priority, state, payload_json)
                          VALUES (?, ?, ?, ?, ?)",
                        entityId,
                        goalKey,
                        0.0,
                        DefaultGoalState,
                        "{}"
                    );
                    inserted++;
                }
            }

            return inserted;
        }

        private static IEnumerable<KeyValuePair<string, List<long>>> GroupEntitiesByHouse(
            ContentCatalog catalog,
            IReadOnlyDictionary<string, long> entityIdByTemplateKey
        )
        {
            var byHouse = new Dictionary<string, List<long>>();
            foreach (EntityTemplateDefinition template in catalog.EntityTemplates.Values)
            {
                if (string.IsNullOrWhiteSpace(template.houseKey))
                {
                    continue;
                }

                if (!entityIdByTemplateKey.TryGetValue(template.key, out long entityId))
                {
                    continue;
                }

                if (!byHouse.TryGetValue(template.houseKey, out List<long> members))
                {
                    members = new List<long>();
                    byHouse.Add(template.houseKey, members);
                }

                members.Add(entityId);
            }

            var ordered = new List<KeyValuePair<string, List<long>>>(byHouse);
            ordered.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            foreach (KeyValuePair<string, List<long>> house in ordered)
            {
                house.Value.Sort();
            }

            return ordered;
        }

        private static void AccumulateOpinion(
            Dictionary<long, Dictionary<long, int>> opinionBySubjectTarget,
            long subjectId,
            long targetId,
            int value
        )
        {
            if (!opinionBySubjectTarget.TryGetValue(subjectId, out Dictionary<long, int> byTarget))
            {
                byTarget = new Dictionary<long, int>();
                opinionBySubjectTarget.Add(subjectId, byTarget);
            }

            byTarget.TryGetValue(targetId, out int existing);
            byTarget[targetId] = existing + value;
        }

        private static List<KeyValuePair<long, TValue>> Ordered<TValue>(Dictionary<long, TValue> source)
        {
            var ordered = new List<KeyValuePair<long, TValue>>(source);
            ordered.Sort((left, right) => left.Key.CompareTo(right.Key));
            return ordered;
        }

        private static void InsertRelationshipTerm(
            SQLiteConnection connection,
            long subjectId,
            long targetId,
            string type,
            int value,
            string payloadJson
        )
        {
            connection.Execute(
                @"INSERT INTO relationship_terms (
                    subject_entity_id,
                    target_entity_id,
                    type,
                    value,
                    decay_per_tick,
                    expires_game_tick,
                    payload_json
                  )
                  VALUES (?, ?, ?, ?, 0, NULL, ?)",
                subjectId,
                targetId,
                type,
                value,
                payloadJson
            );
        }

        private static void InsertRelationship(SQLiteConnection connection, long subjectId, long targetId, int opinion)
        {
            connection.Execute(
                @"INSERT INTO relationships (
                    subject_entity_id,
                    target_entity_id,
                    opinion,
                    familiarity,
                    updated_game_tick
                  )
                  VALUES (?, ?, ?, 0, 0)",
                subjectId,
                targetId,
                opinion
            );
        }

        private static string KinshipPayload(string kinshipType)
        {
            return string.Format("{{\"kinship\":\"{0}\"}}", kinshipType ?? string.Empty);
        }

        private static string HousePayload(string houseKey)
        {
            return string.Format("{{\"house\":\"{0}\"}}", houseKey ?? string.Empty);
        }

        private static int ClampOpinion(int opinion)
        {
            return Math.Max(-100, Math.Min(100, opinion));
        }

        private sealed class SocialSeedCounts
        {
            public SocialSeedCounts(int relationshipsInserted, int relationshipTermsInserted)
            {
                RelationshipsInserted = relationshipsInserted;
                RelationshipTermsInserted = relationshipTermsInserted;
            }

            public int RelationshipsInserted { get; }
            public int RelationshipTermsInserted { get; }
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
