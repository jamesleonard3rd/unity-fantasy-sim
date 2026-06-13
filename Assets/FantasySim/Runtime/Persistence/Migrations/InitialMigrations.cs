using System.Collections.Generic;

namespace FantasySim.Persistence.Migrations
{
    public static class InitialMigrations
    {
        public static IReadOnlyList<SqlMigration> All { get; } = new[]
        {
            new SqlMigration(
                "001_initial_save_state",
                "Create initial save-state schema.",
                new[]
                {
                    @"CREATE TABLE IF NOT EXISTS world_clock (
                        id INTEGER PRIMARY KEY CHECK (id = 1),
                        game_tick INTEGER NOT NULL,
                        ticks_per_game_day INTEGER NOT NULL,
                        updated_utc TEXT NOT NULL
                    )",
                    @"CREATE TABLE IF NOT EXISTS regions (
                        id INTEGER PRIMARY KEY,
                        key TEXT UNIQUE NOT NULL,
                        display_name TEXT NOT NULL,
                        type TEXT NOT NULL,
                        parent_id INTEGER NULL,
                        tick_interval_seconds REAL NOT NULL,
                        last_tick_game_tick INTEGER NOT NULL DEFAULT 0,
                        authority_mode TEXT NOT NULL DEFAULT 'coarse',
                        authority_owner TEXT NULL,
                        authority_until_tick INTEGER NULL,
                        FOREIGN KEY (parent_id) REFERENCES regions(id)
                    )",
                    @"CREATE TABLE IF NOT EXISTS simulation_partitions (
                        id INTEGER PRIMARY KEY,
                        key TEXT UNIQUE NOT NULL,
                        display_name TEXT NOT NULL,
                        tick_interval_seconds REAL NOT NULL,
                        last_tick_game_tick INTEGER NOT NULL DEFAULT 0,
                        authority_mode TEXT NOT NULL DEFAULT 'coarse'
                    )",
                    @"CREATE TABLE IF NOT EXISTS simulation_partition_regions (
                        partition_id INTEGER NOT NULL,
                        region_id INTEGER NOT NULL,
                        PRIMARY KEY (partition_id, region_id),
                        FOREIGN KEY (partition_id) REFERENCES simulation_partitions(id) ON DELETE CASCADE,
                        FOREIGN KEY (region_id) REFERENCES regions(id) ON DELETE CASCADE
                    )",
                    @"CREATE TABLE IF NOT EXISTS entities (
                        id INTEGER PRIMARY KEY,
                        template_key TEXT NULL,
                        display_name TEXT NOT NULL,
                        race_key TEXT NULL,
                        subrace_key TEXT NULL,
                        age_ticks INTEGER NOT NULL DEFAULT 0,
                        is_alive INTEGER NOT NULL DEFAULT 1,
                        payload_json TEXT
                    )",
                    @"CREATE TABLE IF NOT EXISTS entity_positions (
                        entity_id INTEGER PRIMARY KEY,
                        region_id INTEGER NOT NULL,
                        local_x REAL NULL,
                        local_y REAL NULL,
                        local_z REAL NULL,
                        heading_y REAL NULL,
                        FOREIGN KEY (entity_id) REFERENCES entities(id) ON DELETE CASCADE,
                        FOREIGN KEY (region_id) REFERENCES regions(id)
                    )",
                    @"CREATE TABLE IF NOT EXISTS entity_needs (
                        entity_id INTEGER PRIMARY KEY,
                        hunger REAL NOT NULL,
                        fatigue REAL NOT NULL,
                        mood REAL NOT NULL,
                        stress REAL NOT NULL,
                        FOREIGN KEY (entity_id) REFERENCES entities(id) ON DELETE CASCADE
                    )",
                    @"CREATE TABLE IF NOT EXISTS relationships (
                        subject_entity_id INTEGER NOT NULL,
                        target_entity_id INTEGER NOT NULL,
                        opinion INTEGER NOT NULL,
                        familiarity INTEGER NOT NULL DEFAULT 0,
                        updated_game_tick INTEGER NOT NULL,
                        PRIMARY KEY (subject_entity_id, target_entity_id),
                        FOREIGN KEY (subject_entity_id) REFERENCES entities(id) ON DELETE CASCADE,
                        FOREIGN KEY (target_entity_id) REFERENCES entities(id) ON DELETE CASCADE
                    )",
                    @"CREATE TABLE IF NOT EXISTS relationship_terms (
                        id INTEGER PRIMARY KEY,
                        subject_entity_id INTEGER NOT NULL,
                        target_entity_id INTEGER NOT NULL,
                        kind TEXT NOT NULL,
                        value INTEGER NOT NULL,
                        decay_per_tick REAL NOT NULL DEFAULT 0,
                        expires_game_tick INTEGER NULL,
                        payload_json TEXT,
                        FOREIGN KEY (subject_entity_id) REFERENCES entities(id) ON DELETE CASCADE,
                        FOREIGN KEY (target_entity_id) REFERENCES entities(id) ON DELETE CASCADE
                    )",
                    @"CREATE TABLE IF NOT EXISTS world_events (
                        id INTEGER PRIMARY KEY,
                        region_id INTEGER NULL,
                        kind TEXT NOT NULL,
                        subject_entity_id INTEGER NULL,
                        target_entity_id INTEGER NULL,
                        significance INTEGER NOT NULL,
                        payload_json TEXT NOT NULL,
                        occurred_game_tick INTEGER NOT NULL,
                        created_utc TEXT NOT NULL,
                        FOREIGN KEY (region_id) REFERENCES regions(id),
                        FOREIGN KEY (subject_entity_id) REFERENCES entities(id),
                        FOREIGN KEY (target_entity_id) REFERENCES entities(id)
                    )",
                    @"CREATE INDEX IF NOT EXISTS idx_partitions_due
                        ON simulation_partitions (authority_mode, last_tick_game_tick)",
                    @"CREATE INDEX IF NOT EXISTS idx_entity_positions_region
                        ON entity_positions (region_id)",
                    @"CREATE INDEX IF NOT EXISTS idx_world_events_region_tick
                        ON world_events (region_id, occurred_game_tick)",
                    @"CREATE INDEX IF NOT EXISTS idx_relationship_terms_pair
                        ON relationship_terms (subject_entity_id, target_entity_id)"
                }
            )
        };
    }
}
