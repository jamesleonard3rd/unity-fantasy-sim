# Fantasy Sim Unity Roadmap

This roadmap describes how to rebuild the current fantasy world simulation as a
Unity/C# project starting from zero.

Assumptions:

- The Unity project starts with only a basic SQLite-backed persistence service.
- The current Python codebase is reference material, not code to port directly.
- The JSON files under `game_data/` are the main assets worth transferring.
- The target is a persistent fantasy world with hundreds to thousands of NPCs,
  coarse simulation for unloaded regions, and full Unity gameplay for the region
  the player is currently inside.
- The world only needs to advance while a player session is active. No offline
  daemon is required while nobody is playing.

Do not carry over Python-specific concerns such as FastAPI views, psycopg access
patterns, pytest setup, Postgres tuning, Python package layout, mypy cleanup, or
Python runtime performance escape hatches. Unity owns both presentation and the
session runtime in this version.

---

## Current Implementation Status

The foundation-first setup has started in this Unity project.

Implemented:

- `Assets/FantasySim/Runtime/Core/` contains initial ID, time, random, and
  validation result primitives.
- `Assets/FantasySim/Runtime/Content/` contains the first static content
  boundary: definition DTOs, JSON loaders, key normalization and validation
  helpers, `ContentCatalog`, `ContentCatalogBuilder`, and validation report
  formatting for editor tooling.
- `Assets/FantasySim/Runtime/Persistence/` owns SQLite path resolution,
  connection setup, database initialization, migration execution, and database
  status reporting. It can now generate the first starter-world save-state rows
  from a validated `ContentCatalog`.
- `Assets/FantasySim/Runtime/Simulation/` contains the first pure tick path:
  runtime state DTOs, a starter simulation pipeline, a
  `RelationshipDynamicsSystem` that decays relationship terms and recalculates
  cached opinions, and a `RegionHeartbeatSystem` that emits a
  low-significance `region.tick` event. `Assets/FantasySim/Runtime/Session/`
  remains an assembly boundary for later session orchestration.
- `Assets/FantasySim/Editor/DatabaseTools/` exposes editor menu items for
  validating game data, rebuilding the development database, generating the
  starter world, and inspecting database status.
- `Assets/FantasySim/Tests/EditMode/` contains initial foundation tests for
  database initialization, migration idempotency, foreign keys, content key
  validation, JSON loading, reference validation, catalog creation, validation
  report formatting, starter-world generation/idempotency, partition loading,
  relationship loading, relationship delta persistence, relationship dynamics,
  heartbeat simulation, and heartbeat event persistence.
- `Assets/DB.cs` is now only a temporary bootstrap component that delegates to
  `DatabaseInitializer`.

Current verification:

- Unity opens the project and compiles the new assemblies.
- EditMode tests pass from the Unity Test Runner.
- `Fantasy Sim/Database/Rebuild Development Database` creates a fresh
  development database.
- `Fantasy Sim/Database/Log Database Status` reports the initial schema
  migration once: `001_initial_save_state`.
- Static JSON content has been moved into `Assets/GameData/Json/`.
- JSON loaders now parse factions, races, stats, traits, regions, and entity
  templates into normalized definition DTOs.
- Static definitions are intentionally not mirrored into SQLite yet. JSON plus
  `ContentCatalog` remain the canonical immutable content source; SQLite stores
  generated save-state rows and stable definition keys.
- `Fantasy Sim/Validate Game Data` builds the catalog and logs counts plus
  actionable validation messages.
- `Fantasy Sim/Database/Generate Starter World` writes the initial clock,
  regions, starter simulation partition, entity rows, and positions into the
  development database when it is empty.
- Runtime state DTOs now represent a loaded partition in memory, including
  relationship rows, relationship terms, and relationship delta payloads.
- `StarterWorldReadRepository` loads the generated `starter_world` partition,
  including regions, entities, positions, relationship pairs, relationship
  terms, and clock context.
- `Fantasy Sim/Simulation/Tick Starter World` loads the starter partition, runs
  relationship dynamics ahead of the heartbeat, persists changed relationship
  opinions/terms plus events, and advances the world clock.
- The development starter world has been generated from the editor menu, and
  database status reports nonzero counts for generated world tables.
- CLI EditMode test execution for the relationship milestone was attempted, but
  Unity batchmode was blocked because another Unity instance has this project
  open. Re-run from the Unity Editor/Test Runner or after closing the open
  Editor instance.

Next immediate milestone:

1. Seed starter-world relationships and permanent kinship/house terms from
   entity template metadata so generated worlds have meaningful social state.
2. Add the first `GoalScoringSystem` data model and runtime system, keeping goal
   state typed and delta-tracked like relationships.
3. Extend the SQLite load/write path for active goal rows without rewriting
   unchanged entities or unrelated relationships.
4. Cover seeded relationship data, goal loading, deterministic goal scoring, and
   goal delta writes with focused EditMode tests.

---

## 1. Product Target

Build a persistent fantasy world inhabited by many NPCs, factions, houses,
schools, regions, relationships, items, traits, and abilities.

The player experiences one live region at a time in Unity. That live region runs
frame-by-frame gameplay: navigation, animation, combat, dialogue, interaction,
local AI, perception, and moment-to-moment state.

All other regions are simulated at a coarse interval while the player session is
active. Coarse simulation should be good enough to make the world feel alive when
the player travels: relationships drift, factions maneuver, schools enroll or
graduate students, NPCs age, events happen, and summaries can be shown
on region entry.

Persistence stores saved state between sessions. It should not be treated as the
live gameplay engine. Load from SQLite at session start or region entry, simulate
in C# memory, and save back in batches at checkpoints, region exits, and session
end.

---

## 2. Unity Architecture Overview

Keep these responsibilities separate even though they all live in one Unity
project at first:

1. **Static content**
   - JSON files imported from `game_data/`.
   - Definitions for races, traits, stats, factions, regions, schools, goals,
     items, abilities, names, houses, and generation templates.
   - Loaded into typed C# definition classes.
   - Seeded into SQLite or converted to Unity assets during editor import.

2. **Persistence**
   - SQLite database stored in the player save location.
   - Owns durable world state between sessions.
   - Stores static lookup tables, entity component tables, world clock,
     region metadata, relationship terms, and event history.
   - Accessed through repository classes, never directly from gameplay systems.

3. **Session runtime**
   - C# services created when the player starts or loads a world.
   - Loads world state into memory.
   - Owns the world clock and region tick scheduler.
   - Runs coarse simulation for unloaded regions.
   - Coordinates live-region entry and exit.
   - Writes checkpoints and final saves.

4. **Live region gameplay**
   - Unity scene or additive scene currently occupied by the player.
   - Owns GameObjects, MonoBehaviours, animation, physics, NavMesh, combat, UI,
     input, camera, audio, and real-time AI.
   - Hydrates from runtime state on entry.
   - Sends meaningful changes back to the session runtime on exit or checkpoint.

5. **Simulation systems**
   - Pure C# logic over in-memory region state.
   - No UnityEngine dependencies where possible.
   - No SQLite calls.
   - Deterministic with an injected RNG and world clock.
   - Testable with EditMode tests.

The core rule: Unity visuals and SQLite persistence are adapters around a
simulation model. Do not bury world rules inside MonoBehaviours or SQL queries.

---

## 3. Scale Targets

Initial targets:

- World state: 2,000 to 5,000 entities total.
- Region count: 20 to 30 regions. Regions divided into subregions where entity simulation only touches other entities in their subregion.
- Average region population: 100 to 250 entities.
- Hard design cap per coarse region: about 300 entities.
- Live region population: typically 50 to 100 entities, peak 150 to 200.
- Coarse unloaded-region tick interval: 180 seconds by default.
- Session-only simulation: no ticks while no player session is running.

These are comfortable numbers for C# if the data model stays disciplined. The
main threat is not C# speed. The main threat is accidental O(N^2) behavior,
excessive allocations, and database calls inside loops.

Design constraints:

- Do not tick every unloaded NPC every frame.
- Do not query SQLite per entity.
- Do not save each entity as a separate transaction.
- Do not let live-region gameplay and coarse simulation write the same region at
  the same time.
- Split crowded places into child regions before they exceed the target cap.

---

## 4. What To Transfer

Transfer the JSON files first:

- `game_data/races/races.json`
- `game_data/traits/traits.json`
- `game_data/stats/stats.json`
- `game_data/factions/factions.json`
- `game_data/items/items.json`
- `game_data/abilities/abilities.json`
- `game_data/schools/*.json`
- `game_data/regions/templates.json`
- `game_data/entities/templates.json`
- `game_data/houses/templates.json`
- `game_data/goals/templates.json`
- `game_data/names/*.json`
- `game_data/rarity/*.json`
- `game_data/config/game_settings.json`

These should become the Unity project's static data source. You can either keep
them as JSON TextAssets or import them into ScriptableObjects. For this project,
start by keeping JSON as the source of truth because it is easy to diff, easy to
regenerate, and already exists.

Recommended approach:

1. Copy `game_data/` into `Assets/GameData/Json/`.
2. Write typed C# data transfer classes for each JSON shape.
3. Add an editor validation tool that loads every JSON file and reports schema
   or reference errors.
4. Build a `ContentCatalog` from JSON and optionally mirror definitions into
   SQLite for debug queries.
5. Later, if Unity authoring becomes more important, generate ScriptableObjects
   from JSON or write editor tools that modify JSON safely.

Do not transfer:

- Python module layout.
- FastAPI debug dashboard.
- Postgres schema syntax directly.
- Python seed scripts.
- Python scheduler implementation.
- psycopg transaction code.
- pytest-specific test harnesses.
- mypy or Python packaging tasks.

The design survives. The implementation should be idiomatic Unity and C#.

---

## 5. Recommended Unity Project Layout

Use assemblies early. They keep dependencies honest and make tests faster. Start
with a smaller set than the eventual architecture needs, then split assemblies
when a boundary becomes real.

```text
Assets/
  FantasySim/
    Runtime/
      Core/
        FantasySim.Core.asmdef
        Ids/
        Time/
        Random/
        Results/
      Content/
        FantasySim.Content.asmdef
        Json/
        Definitions/
        Catalog/
        Validation/
      Persistence/
        FantasySim.Persistence.asmdef
        SQLite/
        Migrations/
        Repositories/
        Save/
      Simulation/
        FantasySim.Simulation.asmdef
        State/
        Systems/
        Scheduling/
        Events/
      Session/
        FantasySim.Session.asmdef
        Bootstrap/
        Services/
        RegionFlow/
        Checkpoints/
      Presentation/
        FantasySim.Presentation.asmdef
        Actors/
        AI/
        Combat/
        Interaction/
        Navigation/
        UI/
        Debug/
    Editor/
      FantasySim.Editor.asmdef
      DataImport/
      Validation/
      DatabaseTools/
      DebugWindows/
    Tests/
      EditMode/
      PlayMode/
  GameData/
    Json/
      ...
```

Initial assembly dependency direction:

- `Core` depends on nothing Unity-specific where possible.
- `Content` depends on `Core`.
- `Persistence` depends on `Core` and `Content`.
- `Simulation` depends on `Core` and `Content`.
- `Session` depends on `Core`, `Persistence`, and `Simulation`.
- `Presentation` depends on session-facing services and Unity packages.
- `Editor` may depend on everything because it is tooling.

Avoid making `Simulation` depend on `UnityEngine`. A pure simulation assembly is
easier to test and can later move to a dedicated server if multiplayer happens.
If `Presentation` grows too large, split it later into `LiveRegion` and `UI`.

---

## 6. SQLite Foundation

Unity does not need a separate database server process for this roadmap. SQLite
is embedded. Treat the "SQLite server" as a Unity-owned persistence service with
a clear API.

First implement:

- `DatabasePathProvider`
  - Resolves database path under `Application.persistentDataPath`.
  - Supports editor override paths for test databases.

- `SqliteConnectionFactory`
  - Opens SQLite connections.
  - Enables foreign keys with `PRAGMA foreign_keys = ON`.
  - Sets WAL mode for better read/write behavior: `PRAGMA journal_mode = WAL`.
  - Sets a busy timeout.

- `MigrationRunner`
  - Tracks applied migrations in a `schema_migrations` table.
  - Applies ordered SQL files or embedded migration strings.
  - Can create a fresh database from zero.
  - Can upgrade an existing save.

- `DatabaseInitializer`
  - Creates the database if missing.
  - Runs migrations.
  - Builds the static `ContentCatalog`.
  - Optionally mirrors definitions into SQLite for debug tooling.
  - Creates initial world state if no save exists.

- `UnitOfWork` or transaction helper
  - Begins a transaction.
  - Runs repository writes.
  - Commits or rolls back.
  - Keeps tick writes batched.

- `PersistenceQueue`
  - Serializes SQLite writes through one service boundary.
  - Prevents concurrent writers from fighting over the save database.
  - Lets simulation or loading move to worker threads later without letting
    arbitrary systems open their own database connections.

Connection rules:

- Repositories may open connections.
- Simulation systems may not.
- MonoBehaviours may not issue SQL directly.
- Live gameplay should ask session services for state, not query SQLite.
- Partition ticks should load a working set, simulate in memory, then write one
  delta in one transaction.
- SQLite writes should flow through one persistence service or queue.

---

## 7. Database Schema Shape

Keep the existing ECS-style idea: `entities` is the spine, and most mutable
gameplay save data is stored as component tables keyed by `entity_id`.

Use stable string keys for static definitions and integer IDs for runtime
entities.

Important Unity adjustment: static definitions do not all need to be duplicated
into SQLite. JSON files, imported ScriptableObjects, or a generated
`ContentCatalog` can be the canonical source for immutable definitions. SQLite
should primarily store save-state rows and references to stable definition keys.
Mirror definitions into SQLite only when it materially helps debug tooling,
queries, or save validation.

Suggested schema groups:

### Static Definitions

- `races`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `description TEXT`

- `subraces`
  - `key TEXT PRIMARY KEY`
  - `race_key TEXT NOT NULL`
  - `display_name TEXT NOT NULL`

- `stats`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `description TEXT`

- `traits`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `description TEXT`
  - `rarity TEXT`

- `trait_stat_modifiers`
  - `trait_key TEXT NOT NULL`
  - `stat_key TEXT NOT NULL`
  - `modifier INTEGER NOT NULL`

- `factions`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `kind TEXT NOT NULL`
  - `parent_key TEXT NULL`
  - `description TEXT`

- `items`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `kind TEXT`
  - `rarity TEXT`
  - `payload_json TEXT`

- `abilities`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `school_key TEXT NULL`
  - `payload_json TEXT`

- `schools`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `faction_key TEXT NULL`
  - `payload_json TEXT`

- `goal_templates`
  - `key TEXT PRIMARY KEY`
  - `display_name TEXT NOT NULL`
  - `payload_json TEXT`

### Region And World Tables

- `regions`
  - `id INTEGER PRIMARY KEY`
  - `key TEXT UNIQUE NOT NULL`
  - `display_name TEXT NOT NULL`
  - `type TEXT NOT NULL`
  - `parent_id INTEGER NULL`
  - `tick_interval_seconds REAL NOT NULL`
  - `last_tick_game_tick INTEGER NOT NULL DEFAULT 0`
  - `authority_mode TEXT NOT NULL DEFAULT 'coarse'`
  - `authority_owner TEXT NULL`
  - `authority_until_tick INTEGER NULL`

- `simulation_partitions`
  - `id INTEGER PRIMARY KEY`
  - `key TEXT UNIQUE NOT NULL`
  - `display_name TEXT NOT NULL`
  - `tick_interval_seconds REAL NOT NULL`
  - `last_tick_game_tick INTEGER NOT NULL DEFAULT 0`
  - `authority_mode TEXT NOT NULL DEFAULT 'coarse'`

- `simulation_partition_regions`
  - `partition_id INTEGER NOT NULL`
  - `region_id INTEGER NOT NULL`

- `travel_edges`
  - `id INTEGER PRIMARY KEY`
  - `from_region_key TEXT NOT NULL`
  - `to_region_key TEXT NOT NULL`
  - `mode TEXT NOT NULL`
  - `cost_ticks INTEGER NOT NULL`
  - `payload_json TEXT`

- `world_clock`
  - `id INTEGER PRIMARY KEY CHECK (id = 1)`
  - `game_tick INTEGER NOT NULL`
  - `ticks_per_game_day INTEGER NOT NULL`
  - `updated_utc TEXT NOT NULL`

- `world_events`
  - `id INTEGER PRIMARY KEY`
  - `region_id INTEGER NULL`
  - `kind TEXT NOT NULL`
  - `subject_entity_id INTEGER NULL`
  - `target_entity_id INTEGER NULL`
  - `significance INTEGER NOT NULL`
  - `payload_json TEXT NOT NULL`
  - `occurred_game_tick INTEGER NOT NULL`
  - `created_utc TEXT NOT NULL`

### Entity Tables

- `entities`
  - `id INTEGER PRIMARY KEY`
  - `template_key TEXT NULL`
  - `display_name TEXT NOT NULL`
  - `race_key TEXT NULL`
  - `subrace_key TEXT NULL`
  - `age_ticks INTEGER NOT NULL DEFAULT 0`
  - `is_alive INTEGER NOT NULL DEFAULT 1`
  - `payload_json TEXT`

- `entity_stats`
  - `entity_id INTEGER NOT NULL`
  - `stat_key TEXT NOT NULL`
  - `value INTEGER NOT NULL`

- `entity_traits`
  - `entity_id INTEGER NOT NULL`
  - `trait_key TEXT NOT NULL`

- `entity_factions`
  - `entity_id INTEGER NOT NULL`
  - `faction_key TEXT NOT NULL`
  - `rank TEXT NULL`
  - `reputation INTEGER NOT NULL DEFAULT 0`

- `entity_items`
  - `entity_id INTEGER NOT NULL`
  - `item_key TEXT NOT NULL`
  - `quantity INTEGER NOT NULL DEFAULT 1`

- `entity_abilities`
  - `entity_id INTEGER NOT NULL`
  - `ability_key TEXT NOT NULL`
  - `level INTEGER NOT NULL DEFAULT 1`

- `entity_schools`
  - `entity_id INTEGER NOT NULL`
  - `school_key TEXT NOT NULL`
  - `status TEXT NOT NULL`
  - `started_game_tick INTEGER NULL`
  - `completed_game_tick INTEGER NULL`

- `entity_positions`
  - `entity_id INTEGER PRIMARY KEY`
  - `region_id INTEGER NOT NULL`
  - `local_x REAL NULL`
  - `local_y REAL NULL`
  - `local_z REAL NULL`
  - `heading_y REAL NULL`

- `entity_goals`
  - `id INTEGER PRIMARY KEY`
  - `entity_id INTEGER NOT NULL`
  - `goal_key TEXT NOT NULL`
  - `priority REAL NOT NULL`
  - `state TEXT NOT NULL`
  - `payload_json TEXT`

### Relationship Tables

- `relationships`
  - `subject_entity_id INTEGER NOT NULL`
  - `target_entity_id INTEGER NOT NULL`
  - `opinion INTEGER NOT NULL`
  - `familiarity INTEGER NOT NULL DEFAULT 0`
  - `updated_game_tick INTEGER NOT NULL`

- `relationship_terms`
  - `id INTEGER PRIMARY KEY`
  - `subject_entity_id INTEGER NOT NULL`
  - `target_entity_id INTEGER NOT NULL`
  - `kind TEXT NOT NULL`
  - `value INTEGER NOT NULL`
  - `decay_per_tick REAL NOT NULL DEFAULT 0`
  - `expires_game_tick INTEGER NULL`
  - `payload_json TEXT`

Minimum indexes:

- `idx_partitions_due` on authority/tick fields.
- `idx_entity_positions_region` on `entity_positions(region_id)`.
- `idx_world_events_region_tick` on `world_events(region_id, occurred_game_tick)`.
- `idx_relationships_subject_target`.
- `idx_relationship_terms_pair`.
- `idx_entity_factions_faction`.

SQLite stores booleans as integers and JSON as text. Keep JSON payload columns
for flexible details, but do not hide frequently queried state inside JSON.

---

## 8. C# Data Model

Use small structs or classes for IDs and state. Avoid passing raw strings and
integers everywhere once the basics work.

Examples:

```csharp
public readonly record struct EntityId(long Value);
public readonly record struct RegionId(long Value);
public readonly record struct GameTick(long Value);
```

Definition classes should mirror JSON and feed a read-only content catalog:

```csharp
[Serializable]
public sealed class FactionDefinition
{
    public string key;
    public string name;
    public string kind;
    public string description;
    public string parent;
}
```

```csharp
public sealed class ContentCatalog
{
    public IReadOnlyDictionary<string, FactionDefinition> Factions { get; init; }
    public IReadOnlyDictionary<string, RegionDefinition> Regions { get; init; }
    public IReadOnlyDictionary<string, GoalDefinition> Goals { get; init; }
}
```

Runtime rows should be explicit:

```csharp
public sealed class EntityRecord
{
    public EntityId Id { get; init; }
    public string DisplayName { get; set; }
    public string RaceKey { get; set; }
    public bool IsAlive { get; set; }
    public long AgeTicks { get; set; }
}
```

Region working state should be typed. Do not port the Python habit of passing
`Dictionary<string, object>` rows through every system. In C#, use explicit
records/classes for rows, and keep dictionaries as lookup indexes.

```csharp
public sealed class RegionState
{
    public RegionRecord Region { get; init; }
    public SimulationPartitionRecord Partition { get; init; }
    public List<EntityRecord> Entities { get; } = new();
    public Dictionary<EntityId, int> EntityIndexById { get; } = new();
    public List<RelationshipRecord> Relationships { get; } = new();
    public List<RelationshipTermRecord> RelationshipTerms { get; } = new();

    public RegionStateDelta Delta { get; } = new();
}

public sealed class RegionStateDelta
{
    public HashSet<EntityId> UpdatedEntities { get; } = new();
    public HashSet<RelationshipKey> UpdatedRelationships { get; } = new();
    public HashSet<long> UpdatedRelationshipTerms { get; } = new();
    public List<PendingWorldEvent> Events { get; } = new();
}
```

The delta object replaces scattered dirty sets over time. It gives repositories,
live-region writeback, tests, checkpointing, and future multiplayer code one
clear result shape to inspect and persist.

---

## 9. JSON Import And Validation

Build JSON import before building gameplay systems. Bad data will otherwise
look like bad simulation code.

Steps:

1. Copy JSON files into `Assets/GameData/Json/`.
2. Pick one JSON parser:
   - `System.Text.Json` if your Unity version and target platforms support it
     cleanly.
   - `Newtonsoft.Json` via Unity's package if you need broader JSON features.
   - Unity `JsonUtility` only for simple shapes; it is limited and awkward for
     dictionaries and polymorphic payloads.
3. Create one loader per data group:
   - `RaceJsonLoader` **Done for the first slice.**
   - `TraitJsonLoader` **Done for the first slice.**
   - `StatJsonLoader` **Done for the first slice.**
   - `FactionJsonLoader` **Done for the first slice.**
   - `RegionTemplateJsonLoader` **Done for the first slice.**
   - `EntityTemplateJsonLoader` **Done for the first slice.**
   - `SchoolJsonLoader`
   - `GoalTemplateJsonLoader`
4. Normalize keys during import. **Done for the first-slice loaders.**
   - Display names can contain spaces and punctuation.
   - Keys should be stable, lowercase, and safe for references.
   - Example: `"The White Tower"` -> `the_white_tower`.
5. Validate references. **Started for first-slice content.**
   - Faction parent exists. **Done.**
   - Trait stat modifier references a known stat. **Done.**
   - Ability school exists.
   - Entity template faction exists. **Done.**
   - Region parent exists. **Done.**
   - Travel edge endpoints exist.
6. Validate duplicates. **Done for first-slice content.**
   - No duplicate keys within a table. **Done.**
   - No ambiguous display-name references after normalization. **Done.**
7. Validate gameplay ranges.
   - Rarity is known.
   - Stat values are within expected bounds.
   - Relationship opinions clamp to -100..100.
   - Region caps are not exceeded by generated content.
8. Add an editor menu item: **Pending.**
   - `Fantasy Sim/Validate Game Data`
   - Runs every loader and validator.
   - Prints a clear error list.
   - Fails fast before seeding.
9. Add an editor menu item:
   - `Fantasy Sim/Rebuild Development Database`
   - Deletes the editor test database.
   - Runs migrations.
   - Seeds definitions.
   - Generates or imports initial entities and regions.

Acceptance criteria:

- A fresh Unity checkout can validate all JSON.
- The editor can create a new SQLite database from JSON.
- Invalid references produce actionable errors.
- `ContentCatalog` generation is idempotent.

---

## 10. Repository Layer

Create repositories around use cases, not one giant database class.

Suggested repositories:

- `DefinitionRepository`
  - Optional. Mirrors static data into SQLite only if tooling needs SQL access.
  - Should not become the canonical home of immutable content.

- `ContentCatalogService`
  - Loads JSON or generated assets at startup.
  - Exposes immutable definitions by stable key.
  - Validates save-state references against content definitions.

- `WorldRepository`
  - Reads and writes `world_clock`.
  - Creates a new world.
  - Checks whether a save exists.

- `RegionRepository`
  - Reads region metadata.
  - Reads region hierarchy and authority fields.
  - Sets region authority mode.
  - Updates `last_tick_game_tick`.

- `SimulationPartitionRepository`
  - Maps scheduler partitions to one or more region IDs.
  - Finds due coarse-sim partitions.
  - Updates partition tick metadata.

- `RegionStateRepository`
  - Loads a full partition working set.
  - Writes `RegionStateDelta` after simulation.
  - Uses one transaction per partition tick.

- `EventRepository`
  - Bulk inserts world events.
  - Reads events since a given game tick for a region.
  - Prunes or archives old low-significance events if needed.

- `EntityRepository`
  - Creates generated entities.
  - Reads individual entity summaries for UI.
  - Handles live-region write-back.

Repository rules:

- Load partition state with wide queries filtered by included region IDs.
- Do not query inside per-entity loops.
- Write deltas with prepared statements.
- Wrap region writes and event inserts in one transaction.
- Keep SQL out of simulation systems.
- Keep SQL out of MonoBehaviours.

---

## 11. Session Runtime

The session runtime exists only while a world is loaded.

Core services:

- `GameSessionService`
  - Starts a new session.
  - Loads or creates the world.
  - Owns lifecycle for scheduler, live-region controller, and checkpoints.
  - Stops simulation and saves on quit.

- `WorldClockService`
  - Holds current game tick in memory.
  - Converts ticks to calendar/day/hour display.
  - Advances during simulation.
  - Persists at checkpoint boundaries.

- `CoarseRegionScheduler`
  - Tracks unloaded simulation partitions that are due.
  - Staggers ticks so work is spread out.
  - Skips live-authority partitions or regions.
  - Runs ticks on a controlled cadence.

- `RegionSimulationService`
  - Loads a partition working set.
  - Creates `TickContext`.
  - Runs registered simulation systems.
  - Writes the resulting `RegionStateDelta`.
  - Returns a `TickResult` for logs/debug UI.

- `LiveRegionAuthorityService`
  - Marks a region as live when the player enters.
  - Prevents coarse simulation from writing that region.
  - Accepts write-back when the player leaves.
  - Returns the region to coarse simulation.

- `CheckpointService`
  - Saves pending session deltas periodically.
  - Saves on region transition.
  - Saves on application pause/quit.

Do not make a permanently running world daemon. The session runtime starts with
the game session and stops with it.

---

## 12. Coarse Region Scheduler

The scheduler should be simple at first.

Behavior:

1. On session start, load all simulation partitions eligible for coarse
   simulation.
2. For each partition, compute `nextDueGameTick`.
3. Store due partitions in a priority queue ordered by due tick or due real time.
4. Each scheduler update, check whether the next partition is due.
5. If due, run one partition tick.
6. Reschedule that partition at `now + tick_interval`.
7. Skip partitions or regions whose authority mode is `live`.
8. Refresh the queue periodically in case partitions were added or authority
   changed.

In Unity, the scheduler can be implemented as:

- A coroutine for the first version.
- An async task if the SQLite library and threading model are clean.
- A service ticked from a single `GameSessionBehaviour`.

First implementation recommendation:

- Keep all SQLite access on the main thread until correctness is proven.
- Run at most one coarse partition tick per scheduler slice.
- Budget the tick frequency so the player never sees a frame hitch.
- Once stable, move database load/save or heavy simulation to a worker thread if
  profiling says it is necessary.

If using worker threads:

- Do not touch UnityEngine objects off the main thread.
- Keep simulation state as plain C# objects.
- Marshal results back to the main thread before updating UI or GameObjects.
- Use a single writer queue for SQLite writes.

---

## 13. Tick Pipeline

A coarse tick runs a simulation partition. For the first build, a partition may
contain exactly one region. Keeping the partition concept explicit avoids
special cases later for cities, castles, nested child regions, and streamed
subregions.

A coarse tick should follow the same shape every time:

1. Check authority.
   - If the partition is live, skip.
   - If any included region is live-owned, exclude it or skip the partition,
     depending on the partition policy.

2. Load region working set.
   - Region row.
   - Entities in the region.
   - Goals for those entities.
   - Faction memberships.
   - Relationships where both sides are in the region.
   - Relationship terms for those relationships.
   - Any system-specific component rows needed by registered systems.

3. Create tick context.
   - Current game tick.
   - Delta ticks.
   - Deterministic RNG.
   - Partition ID and included region IDs.
   - Configuration values.

4. Run systems in a fixed order.
   - Relationship dynamics.
   - Goal scoring.
   - Pairwise interactions.
   - Faction politics.
   - School lifecycle.
   - Aging/lifecycle.
   - Event summarization.

5. Collect a delta.
   - Each system mutates typed state and records changes in `RegionStateDelta`.
   - Events are part of the delta.
   - Events should be meaningful enough for UI or summaries.

6. Write changed state.
   - Updated relationship rows.
   - Updated relationship term rows.
   - Updated goals.
   - Updated entity rows.
   - New events.
   - Partition `last_tick_game_tick`.

7. Advance or record world clock.
   - The session clock owns time.
   - Region ticks should use the session's current tick.

8. Return result.
   - Partition ID.
   - Entities processed.
   - Systems run.
   - Events emitted.
   - Milliseconds spent.
   - Skip reason if skipped.

Acceptance criteria:

- One partition tick can run from an editor debug button.
- The tick writes at least one event.
- Delta tracking prevents rewriting every row.
- The scheduler can run repeated ticks without duplicate authority conflicts.

---

## 14. Simulation Systems

Use a small interface:

```csharp
public interface IRegionSimulationSystem
{
    string Name { get; }
    void Apply(RegionState state, TickContext context);
}
```

Keep systems pure:

- No SQLite.
- No GameObjects.
- No scene references.
- No `Time.deltaTime`.
- No global random calls.
- No direct UI updates.

Systems should mutate `RegionState` and record changes in `RegionState.Delta`.
They should not return loosely shaped dictionaries or directly write database
rows.

Recommended initial system order:

1. `RelationshipDynamicsSystem`
2. `GoalScoringSystem`
3. `PairwiseInteractionSystem`
4. `FactionPoliticsSystem`
5. `SchoolLifecycleSystem`
6. `AgingLifecycleSystem`
7. `RegionHeartbeatSystem`

The heartbeat system is useful during early development because it proves the
tick path works even before gameplay systems are interesting. It can emit a low
significance `region.tick` event.

## 16. Relationship Dynamics System

Purpose:

- Keep cached relationship opinions explainable.
- Store opinion causes as relationship terms.
- Decay temporary terms.
- Expire old terms.
- Recalculate cached opinions.

Implementation steps:

1. Add `relationships` and `relationship_terms`.
2. Seed starting relationships and kinship terms from entity templates or house
   generation.
3. Load relationships only for pairs where both entities are in the region.
4. For each relationship term:
   - If expired, set value to zero or remove it.
   - If temporary, decay toward zero.
   - Keep permanent kinship or faction terms stable.
5. Sum terms by directed pair.
6. Clamp opinion to -100..100.
7. Update cached `relationships.opinion`.
8. Record changed terms and relationships in `RegionStateDelta`.
9. Emit events only for important changes, not every small drift.

Acceptance criteria:

- Opinion is directional.
- Kinship can affect each direction differently.
- Temporary events decay.
- Cached opinion always matches sum of active terms after the system runs.

---

## 17. Goal Scoring System

Purpose:

- Give each NPC a small set of active goals.
- Score goals based on traits, relationships, faction role, and region.
- Pick a primary goal for the coarse tick.
- Let the selected goal affect state.

Implementation steps:

1. Add `entity_goals`.
2. Import goal templates from JSON.
3. Assign default goals during entity generation.
4. Create `GoalScoringSystem`.
5. For each entity:
   - Load active goals.
   - Compute utility score per goal.
   - Choose primary goal.
   - Apply coarse outcome.
6. Examples:
   - Hungry NPC prioritizes finding food.
   - Ambitious noble prioritizes faction influence.
   - Exhausted guard prioritizes rest.
   - Student prioritizes study.
7. Record updated goals and affected component rows in `RegionStateDelta`.
8. Emit events for significant outcomes.

Keep the first version simple. Utility scoring can be rough; the value is in the
architecture and testability.

---

## 18. Pairwise Interaction System

Purpose:

- Let NPCs in the same region affect each other.
- Create gossip, arguments, friendships, rivalry, mentorship, recruitment, and
  small faction events.

Risk:

- Naive all-pairs interaction becomes expensive as region size grows.

First implementation:

1. For each entity, select up to 30 candidate neighbors.
2. Prefer candidates with:
   - Existing relationship.
   - Same faction.
   - Same school.
   - Same household.
   - Nearby live-region position if known.
3. Score interaction probability.
4. Apply at most a small number of interactions per entity per tick.
5. Represent consequences as relationship terms and world events.

Later optimization:

- Add spatial buckets for regions with local positions.
- Add social buckets for factions, schools, and households.
- Cache candidate lists during a tick.

Acceptance criteria:

- A 250-entity region stays within the tick budget.
- Candidate count is capped.
- No database calls occur inside interaction loops.

---

## 19. Faction Politics System

Purpose:

- Let factions gain or lose reputation.
- Let member actions affect faction relationships.
- Let houses, schools, guilds, orders, and empires exert influence.

Implementation steps:

1. Add faction relationship tables if needed:
   - `faction_relationships`
   - `faction_relationship_terms`
2. Load faction memberships for region entities.
3. Aggregate notable member events.
4. Apply reputation drift.
5. Spill faction changes back into member relationship terms when appropriate.
6. Emit events:
   - `faction.reputation_changed`
   - `faction.skirmish`
   - `faction.alliance_improved`
   - `faction.rivalry_worsened`

Keep this separate from individual relationship dynamics. Factions should
influence NPCs, but NPC relationships should still be understandable on their
own.

---

## 20. School Lifecycle System

Purpose:

- Support schools such as Ember Academy and Celestria University.
- Enroll students.
- Advance study.
- Graduate, expel, or transfer students.
- Unlock abilities or traits.

Implementation steps:

1. Import school JSON.
2. Add school enrollment component rows.
3. Define eligibility rules:
   - Age range.
   - Race/subrace if relevant.
   - Faction/house access.
   - Magic aptitude traits.
   - Wealth or noble status.
4. On tick:
   - Find eligible candidates.
   - Enroll some candidates.
   - Advance enrolled students by elapsed ticks.
   - Resolve graduation or failure.
   - Add abilities or traits on success.
5. Emit events:
   - `school.enrolled`
   - `school.graduated`
   - `school.expelled`
   - `school.ability_learned`

This system validates long-running time-based world state.

---

## 21. Birth, Death, And Aging

Purpose:

- Make the world persistent across long campaigns.
- Let entities enter and leave the simulation.
- Support lineage, houses, inheritance, and historical events.

Implementation steps:

1. Decide time scale.
   - Define ticks per hour/day/month/year.
   - Keep it in `world_clock`.
2. Add age fields.
3. Add lifecycle state.
4. Add family relationship terms.
5. Create `AgingLifecycleSystem`.
6. On tick:
   - Increase age.
   - Apply age thresholds.
   - Resolve rare deaths.
   - Resolve births only at controlled rates.
7. For death:
   - Mark entity dead.
   - Remove from active region sim.
   - Keep historical rows for relationships/events.
8. For birth:
   - Generate entity from parents/house/faction data.
   - Assign region.
   - Add kinship terms.
   - Emit event.

Be conservative. Entity lifecycle touches many tables and should come after the
basic tick loop is reliable.

---

## 22. Live Region Handoff

In the Unity version, there is no Unreal handoff. The handoff is between:

- Coarse unloaded-region simulation.
- Live Unity scene simulation.

Contract:

1. Before entering a region:
   - Set region authority to `live` with an explicit owner such as
     `player_session`.
   - Stop coarse ticks for that region or its containing partition.
   - Load region state from runtime memory or SQLite.
   - Spawn GameObjects for relevant entities.
   - Apply last known positions, relationships, faction state, and local event
     summaries.

2. While inside the region:
   - Unity GameObjects and live AI own moment-to-moment behavior.
   - SQLite is not queried per frame.
   - Important changes are recorded in memory as `RegionStateDelta` entries or
     live-region command results.
   - World events can be appended through the session service.

3. On checkpoint or exit:
   - Collect meaningful state from live actors.
   - Convert GameObject state into durable component rows.
   - Save positions, health/alive status, inventory, relationships, goals, and
     events.
   - Despawn or unload the live scene.
   - Set authority back to `coarse` after write-back succeeds.

4. If the player quits while in a live region:
   - Save live-region deltas first.
   - Save world clock.
   - Save pending events.
   - Close the session cleanly.

Never let the coarse scheduler and live scene both write the same region.

---

## 23. Live Region Implementation

Start with a plain GameObject-based implementation. Do not begin with DOTS unless
profiling proves it is needed.

Core components:

- `EntityView`
  - Holds `EntityId`.
  - Links GameObject to runtime entity state.

- `ActorVitals`
  - Health, stamina, temporary combat state.
  - Converts to durable state only at save boundaries.

- `ActorRelationshipAdapter`
  - Gives dialogue/AI access to opinions.
  - Records meaningful changes as relationship terms.

- `ActorFactionAdapter`
  - Exposes faction membership and reputation.

- `LiveRegionController`
  - Spawns actors.
  - Owns live-region state.
  - Captures `RegionStateDelta` write-back.

- `RegionSceneLoader`
  - Loads additive scenes or prefabs for regions.
  - Handles transitions.

First live-region slice:

1. Create one test region scene.
2. Spawn player.
3. Spawn 10 NPC capsules from region state.
4. Display names above NPCs.
5. Allow simple click interaction.
6. Show relationship/opinion summary in UI.
7. Save NPC positions on exit.
8. Re-enter and confirm positions persist.

Only after this works should you add combat, complex AI, or dialogue.

---

## 24. Event Log And Player Summaries

The event log is what makes unloaded simulation visible.

Events should be appended for things the player may care about:

- A faction's reputation changed.
- A noble house gained influence.
- A student enrolled or graduated.
- An NPC died or was born.
- A rivalry worsened.
- A region suffered a skirmish.
- A known NPC moved.
- A rare item changed hands.

Event fields:

- Kind.
- Region.
- Subject entity.
- Target entity.
- Significance.
- Occurred game tick.
- JSON payload.

On region entry:

1. Read events for that region since the player last visited.
2. Filter low-significance events.
3. Group similar events.
4. Show a concise summary:
   - "While you were away..."
   - "House Vaelor gained influence in Ironwatch."
   - "Two students graduated from Ember Academy."
   - "Captain Kael's rivalry with Mira deepened."
5. Mark the region as seen up to the current tick.

Do not rely on raw state changes alone. If an NPC opinion changed from 10 to -40,
the event or relationship terms should explain why.

---

## 25. World Clock

Use a deterministic game clock.

The clock should:

- Use integer ticks.
- Have configurable ticks per day.
- Convert ticks to calendar values for UI.
- Be passed into simulation systems through `TickContext`.
- Be saved to SQLite at checkpoint boundaries.

Do not use `DateTime.Now` or `Time.time` inside simulation systems. Real time can
drive when the scheduler wakes up, but game time should be explicit and testable.

Example:

```csharp
public readonly struct TickContext
{
    public GameTick CurrentTick { get; init; }
    public long DeltaTicks { get; init; }
    public int RegionSeed { get; init; }
    public IDeterministicRandom Random { get; init; }
}
```

---

## 26. Deterministic Randomness

Simulation needs randomness, but it should be repeatable for tests.

Implement:

- `IDeterministicRandom`
- `UnityRandomAdapter` only for live gameplay if needed.
- `SeededRandom` for simulation systems.

Rules:

- Systems receive RNG through `TickContext`.
- Systems do not call `UnityEngine.Random` directly.
- Tests provide a fixed seed.
- Region ticks can derive seed from world seed, region ID, and game tick.

This makes simulation failures reproducible.

---

## 27. Editor Tools

Editor tooling is essential because this is a data-heavy game.

Build these tools in order:

1. **Validate Game Data**
   - Loads all JSON.
   - Validates references and duplicates.
   - Shows errors in a clear editor window.

2. **Rebuild Development Database**
   - Deletes editor database.
   - Runs migrations.
   - Seeds definitions.
   - Generates starter regions/entities.

3. **Database Status Window**
   - Shows database path.
   - Shows migration version.
   - Shows entity count.
   - Shows region count.
   - Shows world clock.

4. **Region Debug Window**
   - Lists regions.
   - Shows population.
   - Shows authority mode.
   - Shows last tick.
   - Button to tick one partition.
   - Button to open event history.

5. **Entity Inspector**
   - Search by name.
   - Show traits, faction memberships, goals, relationships, items, abilities,
     and current region.

6. **Event Log Viewer**
   - Filter by region, entity, kind, significance, and tick range.

These tools replace the need for a Python/FastAPI debug dashboard.

---

## 28. Testing Strategy

Use Unity Test Framework.

EditMode tests:

- JSON loaders parse every file.
- Data validators catch missing references.
- Migrations create every table.
- Starter world generation is idempotent.
- Repositories load and write expected rows.
- Relationship system decays terms and clamps opinions.
- Goal scoring selects expected goals.
- Pairwise interaction caps candidate counts.
- Partition tick writes deltas and events.

PlayMode tests:

- New session starts and creates/loads database.
- Entering a region sets authority to live.
- Exiting a region writes live state back.
- Scheduler skips live region.
- Re-entering a region restores saved positions.

Manual smoke tests:

- Rebuild database from editor.
- Tick one region from debug window.
- Start game.
- Enter region.
- Interact with NPC.
- Exit region.
- Restart game.
- Confirm state persists.

Testing rule:

- Most simulation tests should not require Unity scenes.
- Scene tests should cover integration, not every rule.

---

## 29. Minimum Viable First Slice

Build this first before adding deeper simulation.

### Slice Goal

From a fresh Unity project, create a database from JSON, generate a small world,
tick one unloaded region, write an event, enter a live region, save state, and
load it again.

### Tasks

1. Create Unity project structure and assemblies. **Done.**
2. Add SQLite package/plugin and prove a database file can be created. **Done.**
3. Implement migrations. **Done.**
4. Create initial schema. **Done for the first save-state tables:**
   - `schema_migrations`
   - `world_clock`
   - `simulation_partitions`
   - `simulation_partition_regions`
   - `regions`
   - `entities`
   - `entity_positions`
   - `relationships`
   - `relationship_terms`
   - `world_events`
   - only the static definition tables that are useful for validation/debug SQL
5. Copy JSON into `Assets/GameData/Json/`. **Done.**
6. Implement JSON loaders for: **Done for the first slice.**
   - factions
   - races
   - stats
   - traits
   - regions
   - entity templates
7. Implement validation. **Started with generic key validation, normalized
   display-name validation, and first-slice reference validation.**
8. Implement `ContentCatalog` creation from JSON. **Done for the first slice;
   `ContentCatalogBuilder` populates the catalog from loaded definitions.**
9. Implement starter world generation: **Started for the first generated
   save-state slice.**
   - Create 3 regions.
   - Create 3 matching simulation partitions.
   - Create 50 NPCs.
   - Assign NPCs to regions.
   - Give some NPCs relationships.
10. Implement `RegionState`. **Done for the current partition state slice.**
11. Implement `RegionStateDelta`. **Done for events and relationship deltas.**
12. Implement `RegionStateRepository.Load(partitionId)`. **Done as
    `StarterWorldReadRepository.LoadPartition`.**
13. Implement delta tracking. **Started with event, relationship, and
    relationship-term deltas.**
14. Implement `RegionStateRepository.Write(state.Delta)`. **Done for heartbeat
    events, relationship rows, relationship terms, deleted relationship terms,
    and clock advancement.**
15. Implement `TickContext`. **Done for current tick, partition metadata, and
    clock settings.**
16. Implement `RegionHeartbeatSystem`. **Done.**
17. Implement `RelationshipDynamicsSystem`. **Done for deterministic term decay,
    expiry, opinion recalculation, clamping, significant events, and pipeline
    ordering ahead of heartbeat.**
18. Implement `RegionSimulationService.TickPartition(partitionId)`.
19. Implement debug editor button: tick selected partition.
20. Implement `CoarseRegionScheduler`.
21. Implement event log viewer.
22. Implement one live test scene.
23. Spawn NPC placeholders from region state.
24. Save NPC positions on exit through live-region delta write-back.
25. Reload session and verify persisted state.

### Done Criteria

- A new database can be created from inside Unity. **Infrastructure exists;
  verify from an authenticated Unity Editor session.**
- JSON definitions load into a `ContentCatalog` without manual SQL. **Implemented
  for first-slice content; verify in an authenticated Unity Editor session.**
- A partition tick changes at least one persisted value. **Implemented for
  relationship opinions/terms when relationship deltas are present.**
- A partition tick appends at least one event. **Implemented by heartbeat and
  significant relationship opinion changes.**
- The scheduler can tick unloaded partitions while a session is active.
- The scheduler skips the live region.
- The player can enter and exit one region scene.
- NPC positions persist across restart.
- EditMode tests cover JSON validation, content catalog generation, repository
  load/write, and the first simulation systems. **Foundation, first-slice
  content, starter-world repository, heartbeat, and relationship dynamics tests
  exist; rerun the latest relationship tests from Unity after closing the open
  Editor instance that blocks batchmode.**

---

## 30. Development Order After MVP

Build in this order:

1. **Relationship Terms**
   - Explainable opinion.
   - Temporary decay.
   - Kinship terms.
   - Faction terms.

2. **Goals**
   - Active goals per entity.
   - Utility scoring.
   - Primary goal outcome.

3. **Pairwise Interactions**
   - Candidate selection.
   - Relationship changes.
   - Gossip and conflict.

4. **Faction Politics**
   - Faction reputation.
   - House influence.
   - School/guild/order relationships.

5. **School Lifecycle**
   - Enrollment.
   - Study progress.
   - Graduation.
   - Ability unlocks.

6. **Travel**
   - Prebuilt `TravelGraph`.
   - Local travel inside cities/castles.
   - NPC relocation.
   - Player region transitions.

7. **World Processes**
   - Shared process/activity layer for long-running structures.
   - First candidates: tournaments, school terms, faction campaigns, and travel
     plans.
   - Keep the abstraction small until at least two systems genuinely share it.

8. **Birth, Death, Aging**
   - Long-term persistence.
   - Family relationships.
   - Historical event trail.

9. **Live AI Integration**

- Translate coarse goals into live behaviors.
- Let live actions create relationship terms and events.

10. **Player-Facing UI**
    - Region summary.
    - NPC details.
    - Event history.
    - Faction/house/school screens.

---

## 31. Travel Graph

Use authored travel edges plus derived local movement.

Region rules:

- Every region has a stable key.
- Display names are not stable identifiers.
- Regions can have parent regions.
- Cities, castles, settlements, and towns can contain child regions.
- Child regions inside the same local root can be reachable without explicit
  all-to-all edges.

Implementation:

1. Import region templates.
2. Store `regions.key`, `regions.parent_id`, and `regions.type`.
3. Store explicit `travel_edges`.
4. Build a `TravelGraph` service at content import or session start:
   - Explicit edges from JSON/content catalog.
   - Derived local edges among child regions under the same local root.
   - Runtime lookup maps from stable keys to save-state region IDs.
5. Use keys for authoring.
6. Use integer IDs for save-state lookup.
7. Cache the graph for route queries; do not parse JSON or rebuild route data
   during gameplay.

Avoid creating quadratic authored edges inside dense cities. Let the routing
layer derive local connectivity.

---

## 32. Performance Watchpoints

The target scale is modest for Unity/C#, but several patterns can still cause
stutter.

Watch for:

- SQLite reads on every frame.
- SQLite writes for individual entities.
- SQL queries inside per-entity loops.
- Long transactions during scene loading.
- Large allocations during region ticks.
- LINQ-heavy hot loops that allocate every tick.
- Instantiating hundreds of GameObjects at once during visible gameplay.
- Coarse scheduler ticking many regions in the same frame.
- Live scene and coarse simulation writing the same region.
- JSON parsing during gameplay.
- Rebuilding travel graphs during gameplay.

Mitigations:

- Preload or cache definitions.
- Prebuild content indexes and travel graphs at import/session start.
- Load region working sets in batches.
- Use delta tracking.
- Use one transaction per tick.
- Spread scheduler work across time.
- Pool live-region actors where useful.
- Profile before introducing complex threading.
- Add spatial or social buckets before region populations grow too large.

Do not start with DOTS, jobs, or Burst for the whole simulation. Add them only
after profiling identifies a real bottleneck.

---

## 33. Save And Checkpoint Policy

Save at clear boundaries:

- New world creation.
- Region entry after authority changes.
- Region exit after live write-back.
- Periodic checkpoint during active session.
- Application pause.
- Application quit.

Do not save:

- Every frame.
- Every NPC decision.
- Every tiny live movement.
- Every individual row in its own transaction.

Live-region state should be summarized into durable changes. For example:

- Save final position, not every step.
- Save final health/alive state, not every damage preview.
- Save important relationship changes as terms/events.
- Save inventory changes when they become real.
- Save important quest/world flags.

---

## 34. Multiplayer-Friendly Choices

This roadmap is single-player first, but avoid choices that block a future
authoritative server.

Good choices:

- Pure simulation systems.
- Explicit command/result boundaries.
- Runtime services owning authority.
- SQLite hidden behind repositories.
- No gameplay rules inside editor tools.
- No permanent truth stored only in scene objects.
- Event log explaining state changes.

Avoid:

- Client-authoritative permanent state assumptions.
- Direct SQLite calls from UI or actors.
- Simulation logic tied to `MonoBehaviour.Update`.
- Static singletons that make session reset impossible.
- Save formats that depend on scene object instance IDs.

If multiplayer happens later, the pure C# simulation assemblies can move behind a
server process. Unity clients would send commands and receive replicated state.

---

## 35. Quick Start For A Fresh Unity Context

If picking this up cold:

1. Create/open the Unity project.
2. Add the SQLite package/plugin.
3. Create the assembly layout under `Assets/FantasySim/`.
4. Copy `game_data/` to `Assets/GameData/Json/`.
5. Implement migrations and create the first database.
6. Implement JSON loaders and validation.
7. Build the read-only `ContentCatalog` from JSON.
8. Generate a tiny world: 3 regions, 50 NPCs.
9. Implement `RegionState`.
10. Implement `RegionStateDelta`.
11. Implement load/write repositories.
12. Implement one heartbeat tick.
13. Add relationship dynamics.
14. Add editor buttons for validate, rebuild DB, tick partition, and view events.
15. Create one live region scene.
16. Spawn placeholder NPCs.
17. Save and reload positions.
18. Add tests around every non-scene system.

The fastest path is not to build final gameplay first. The fastest path is to
prove the data loop: JSON -> content catalog -> SQLite save state -> in-memory
region state -> simulation -> delta/event writes -> Unity live scene -> save ->
reload.

---

## 36. Definition Of "At This Point"

The Unity project has reached parity with the useful parts of the current
roadmap when:

- Static JSON content imports successfully.
- Immutable definitions are available through a `ContentCatalog`.
- SQLite schema and migrations are owned by Unity.
- A fresh database can be created from zero.
- A generated world contains regions, entities, factions, traits, stats,
  relationships, and world clock state.
- Coarse unloaded-region ticks run during an active session.
- Simulation systems are pure C# and testable without scenes.
- Region ticks write deltas and append events.
- The player can enter one Unity region scene.
- Live-region authority prevents coarse writes to that region.
- Live state can be saved back to persistence.
- Event summaries can explain what happened while the player was elsewhere.
- Tests cover the data import, persistence, and first simulation systems.

At that point, the foundation is ready for richer NPC behavior, faction politics,
school progression, travel, aging, combat, dialogue, and player-facing UI.
