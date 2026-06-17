using System;
using FantasySim.Simulation;
using FantasySim.Simulation.State;
using SQLite;

namespace FantasySim.Persistence.SQLite
{
    public sealed class RegionStateWriteRepository
    {
        private readonly SqliteConnectionFactory connectionFactory;

        public RegionStateWriteRepository(DatabasePathProvider pathProvider = null)
        {
            connectionFactory = new SqliteConnectionFactory(
                pathProvider ?? DatabasePathProvider.ForDefaultGameDatabase()
            );
        }

        public RegionStateWriteResult WritePartitionTick(LoadedPartitionState currentState, PartitionTickDelta delta)
        {
            if (currentState == null)
            {
                throw new ArgumentNullException(nameof(currentState));
            }

            if (delta == null)
            {
                throw new ArgumentNullException(nameof(delta));
            }

            if (delta.TargetTick.Value <= currentState.TickContext.CurrentTick.Value)
            {
                throw new ArgumentException("Target tick must advance beyond the current tick.", nameof(delta));
            }

            int eventsInserted = 0;
            int relationshipsUpdated = 0;
            int relationshipTermsUpdated = 0;
            int relationshipTermsDeleted = 0;
            using SQLiteConnection connection = connectionFactory.OpenConnection();
            connection.RunInTransaction(() =>
            {
                foreach (RegionStateDelta regionDelta in delta.RegionDeltas)
                {
                    foreach (PendingWorldEvent worldEvent in regionDelta.Events)
                    {
                        InsertWorldEvent(connection, worldEvent);
                        eventsInserted++;
                    }

                    foreach (RelationshipState relationship in regionDelta.UpdatedRelationships)
                    {
                        UpsertRelationship(connection, relationship);
                        relationshipsUpdated++;
                    }

                    foreach (RelationshipTermState term in regionDelta.UpdatedRelationshipTerms)
                    {
                        UpsertRelationshipTerm(connection, term);
                        relationshipTermsUpdated++;
                    }

                    foreach (long termId in regionDelta.DeletedRelationshipTermIds)
                    {
                        relationshipTermsDeleted += DeleteRelationshipTerm(connection, termId);
                    }
                }

                int clockRowsUpdated = connection.Execute(
                    @"UPDATE world_clock
                      SET game_tick = ?, updated_utc = ?
                      WHERE id = 1",
                    delta.TargetTick.Value,
                    DateTime.UtcNow.ToString("O")
                );

                if (clockRowsUpdated != 1)
                {
                    throw new InvalidOperationException("Cannot advance world clock because world_clock is missing.");
                }
            });

            return new RegionStateWriteResult(
                currentState.TickContext.CurrentTick,
                delta.TargetTick,
                eventsInserted,
                relationshipsUpdated,
                relationshipTermsUpdated,
                relationshipTermsDeleted
            );
        }

        private static void InsertWorldEvent(SQLiteConnection connection, PendingWorldEvent worldEvent)
        {
            if (worldEvent == null)
            {
                throw new ArgumentNullException(nameof(worldEvent));
            }

            if (string.IsNullOrWhiteSpace(worldEvent.Type))
            {
                throw new ArgumentException("World event type is required.", nameof(worldEvent));
            }

            if (string.IsNullOrWhiteSpace(worldEvent.PayloadJson))
            {
                throw new ArgumentException("World event payload JSON is required.", nameof(worldEvent));
            }

            connection.Execute(
                @"INSERT INTO world_events (
                    region_id,
                    type,
                    subject_entity_id,
                    target_entity_id,
                    significance,
                    payload_json,
                    occurred_game_tick,
                    created_utc
                  )
                  VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                worldEvent.RegionId.HasValue ? (object)worldEvent.RegionId.Value.Value : null,
                worldEvent.Type,
                worldEvent.SubjectEntityId.HasValue ? (object)worldEvent.SubjectEntityId.Value.Value : null,
                worldEvent.TargetEntityId.HasValue ? (object)worldEvent.TargetEntityId.Value.Value : null,
                worldEvent.Significance,
                worldEvent.PayloadJson,
                worldEvent.OccurredTick.Value,
                DateTime.UtcNow.ToString("O")
            );
        }

        private static void UpsertRelationship(SQLiteConnection connection, RelationshipState relationship)
        {
            if (relationship == null)
            {
                throw new ArgumentNullException(nameof(relationship));
            }

            connection.Execute(
                @"INSERT OR REPLACE INTO relationships (
                    subject_entity_id,
                    target_entity_id,
                    opinion,
                    familiarity,
                    updated_game_tick
                  )
                  VALUES (?, ?, ?, ?, ?)",
                relationship.Key.SubjectEntityId.Value,
                relationship.Key.TargetEntityId.Value,
                relationship.Opinion,
                relationship.Familiarity,
                relationship.UpdatedTick.Value
            );
        }

        private static void UpsertRelationshipTerm(SQLiteConnection connection, RelationshipTermState term)
        {
            if (term == null)
            {
                throw new ArgumentNullException(nameof(term));
            }

            if (term.Id > 0)
            {
                connection.Execute(
                    @"INSERT OR REPLACE INTO relationship_terms (
                        id,
                        subject_entity_id,
                        target_entity_id,
                        type,
                        value,
                        decay_per_tick,
                        expires_game_tick,
                        payload_json
                      )
                      VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    term.Id,
                    term.Key.SubjectEntityId.Value,
                    term.Key.TargetEntityId.Value,
                    term.Type,
                    term.Value,
                    term.DecayPerTick,
                    term.ExpiresTick.HasValue ? (object)term.ExpiresTick.Value.Value : null,
                    term.PayloadJson
                );
                return;
            }

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
                  VALUES (?, ?, ?, ?, ?, ?, ?)",
                term.Key.SubjectEntityId.Value,
                term.Key.TargetEntityId.Value,
                term.Type,
                term.Value,
                term.DecayPerTick,
                term.ExpiresTick.HasValue ? (object)term.ExpiresTick.Value.Value : null,
                term.PayloadJson
            );
        }

        private static int DeleteRelationshipTerm(SQLiteConnection connection, long termId)
        {
            if (termId <= 0)
            {
                throw new ArgumentException("Relationship term id must be positive.", nameof(termId));
            }

            return connection.Execute(
                "DELETE FROM relationship_terms WHERE id = ?",
                termId
            );
        }
    }

    public sealed class RegionStateWriteResult
    {
        public RegionStateWriteResult(
            FantasySim.Core.Time.GameTick previousTick,
            FantasySim.Core.Time.GameTick currentTick,
            int eventsInserted,
            int relationshipsUpdated = 0,
            int relationshipTermsUpdated = 0,
            int relationshipTermsDeleted = 0
        )
        {
            PreviousTick = previousTick;
            CurrentTick = currentTick;
            EventsInserted = eventsInserted;
            RelationshipsUpdated = relationshipsUpdated;
            RelationshipTermsUpdated = relationshipTermsUpdated;
            RelationshipTermsDeleted = relationshipTermsDeleted;
        }

        public FantasySim.Core.Time.GameTick PreviousTick { get; }
        public FantasySim.Core.Time.GameTick CurrentTick { get; }
        public int EventsInserted { get; }
        public int RelationshipsUpdated { get; }
        public int RelationshipTermsUpdated { get; }
        public int RelationshipTermsDeleted { get; }
    }
}
