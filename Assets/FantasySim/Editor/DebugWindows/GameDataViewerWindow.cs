using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FantasySim.Content.Catalog;
using FantasySim.Content.Definitions;
using FantasySim.Core.Results;
using FantasySim.Persistence.SQLite;
using UnityEditor;
using UnityEngine;

namespace FantasySim.Editor.DebugWindows
{
    public sealed class GameDataViewerWindow : EditorWindow
    {
        private static readonly string[] Tabs =
        {
            "Overview",
            "Catalog",
            "Starter World",
            "Diagnostics"
        };

        private static readonly string[] CatalogCategories =
        {
            "Factions",
            "Races",
            "Stats",
            "Traits",
            "Regions",
            "Entity Templates"
        };

        private static readonly string[] AliveFilters =
        {
            "All",
            "Alive",
            "Dead"
        };

        private int selectedTab;
        private int selectedCatalogCategory;
        private int selectedEntityIndex = -1;
        private int selectedRegionFilter;
        private int selectedAliveFilter;

        private string catalogSearch = string.Empty;
        private string entitySearch = string.Empty;
        private string diagnostics = string.Empty;
        private Vector2 scrollPosition;

        private ContentCatalogBuildResult catalogBuildResult;
        private DatabaseStatus databaseStatus;
        private LoadedPartitionSnapshot starterWorldState;
        private StarterWorldGenerationResult lastGenerationResult;

        private sealed class LoadedPartitionSnapshot
        {
            public TickContextSnapshot TickContext { get; set; }
            public List<RegionSnapshot> Regions { get; set; }
            public List<EntitySnapshot> Entities { get; set; }
        }

        private sealed class TickContextSnapshot
        {
            public long CurrentTick { get; set; }
            public int TicksPerGameDay { get; set; }
            public long PartitionId { get; set; }
            public string PartitionKey { get; set; }
            public string PartitionDisplayName { get; set; }
            public float PartitionTickIntervalSeconds { get; set; }
            public long PartitionLastTick { get; set; }
        }

        private sealed class RegionSnapshot
        {
            public long Id { get; set; }
            public string Key { get; set; }
            public string DisplayName { get; set; }
            public string Type { get; set; }
            public long? ParentRegionId { get; set; }
            public float TickIntervalSeconds { get; set; }
            public long LastTick { get; set; }
            public string AuthorityMode { get; set; }
            public string AuthorityOwner { get; set; }
            public long? AuthorityUntilTick { get; set; }
        }

        private sealed class EntitySnapshot
        {
            public long Id { get; set; }
            public string TemplateKey { get; set; }
            public string DisplayName { get; set; }
            public string RaceKey { get; set; }
            public string SubraceKey { get; set; }
            public long AgeTicks { get; set; }
            public bool IsAlive { get; set; }
            public string PayloadJson { get; set; }
            public long RegionId { get; set; }
            public float? LocalX { get; set; }
            public float? LocalY { get; set; }
            public float? LocalZ { get; set; }
            public float? HeadingY { get; set; }
        }

        [MenuItem("Fantasy Sim/Game Data Viewer")]
        public static void Open()
        {
            var window = GetWindow<GameDataViewerWindow>("Game Data Viewer");
            window.minSize = new Vector2(760f, 480f);
            window.RefreshAll();
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        private void OnGUI()
        {
            DrawToolbar();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            switch (selectedTab)
            {
                case 0:
                    DrawOverviewTab();
                    break;
                case 1:
                    DrawCatalogTab();
                    break;
                case 2:
                    DrawStarterWorldTab();
                    break;
                default:
                    DrawDiagnosticsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int nextTab = GUILayout.Toolbar(selectedTab, Tabs, EditorStyles.toolbarButton);
            if (nextTab != selectedTab)
            {
                selectedTab = nextTab;
                scrollPosition = Vector2.zero;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                RefreshAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawOverviewTab()
        {
            EditorGUILayout.LabelField("Development Database", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path", DatabasePath);

            if (databaseStatus == null)
            {
                EditorGUILayout.HelpBox("Database status is unavailable. Check Diagnostics for details.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("Applied Migrations", databaseStatus.AppliedMigrations.Count.ToString());
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Table Counts", EditorStyles.boldLabel);

                foreach (KeyValuePair<string, int> tableCount in databaseStatus.TableCounts.OrderBy(pair => pair.Key))
                {
                    EditorGUILayout.LabelField(tableCount.Key, tableCount.Value.ToString());
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
            DrawCatalogSummary();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Starter World", EditorStyles.boldLabel);
            if (starterWorldState == null)
            {
                EditorGUILayout.HelpBox("Starter world is not loaded. Generate it or check Diagnostics for load errors.", MessageType.Info);
            }
            else
            {
                TickContextSnapshot tickContext = starterWorldState.TickContext;
                EditorGUILayout.LabelField("Partition", $"{tickContext.PartitionDisplayName} ({tickContext.PartitionKey})");
                EditorGUILayout.LabelField("Current Tick", tickContext.CurrentTick.ToString());
                EditorGUILayout.LabelField("Regions", starterWorldState.Regions.Count.ToString());
                EditorGUILayout.LabelField("Entities", starterWorldState.Entities.Count.ToString());
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Validate Game Data"))
            {
                RefreshCatalog();
            }

            if (GUILayout.Button("Generate Starter World"))
            {
                GenerateStarterWorld();
            }

            if (GUILayout.Button("Rebuild Development Database"))
            {
                RebuildDevelopmentDatabase();
            }

            EditorGUILayout.EndHorizontal();

            if (lastGenerationResult != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(FormatGenerationResult(lastGenerationResult), MessageType.Info);
            }
        }

        private void DrawCatalogTab()
        {
            DrawCatalogSummary();

            selectedCatalogCategory = GUILayout.Toolbar(selectedCatalogCategory, CatalogCategories);
            catalogSearch = EditorGUILayout.TextField("Search", catalogSearch);

            if (catalogBuildResult == null || catalogBuildResult.Catalog == null)
            {
                EditorGUILayout.HelpBox("Catalog is not loaded. Use Refresh to build it.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            switch (selectedCatalogCategory)
            {
                case 0:
                    DrawFactionCatalog(catalogBuildResult.Catalog.Factions);
                    break;
                case 1:
                    DrawRaceCatalog(catalogBuildResult.Catalog.Races);
                    break;
                case 2:
                    DrawStatCatalog(catalogBuildResult.Catalog.Stats);
                    break;
                case 3:
                    DrawTraitCatalog(catalogBuildResult.Catalog.Traits);
                    break;
                case 4:
                    DrawRegionCatalog(catalogBuildResult.Catalog.Regions);
                    break;
                default:
                    DrawEntityTemplateCatalog(catalogBuildResult.Catalog.EntityTemplates);
                    break;
            }
        }

        private void DrawStarterWorldTab()
        {
            if (starterWorldState == null)
            {
                EditorGUILayout.HelpBox("Starter world is not loaded. Use Generate Starter World from Overview, then Refresh.", MessageType.Info);
                return;
            }

            TickContextSnapshot tickContext = starterWorldState.TickContext;
            EditorGUILayout.LabelField("Partition", $"{tickContext.PartitionDisplayName} ({tickContext.PartitionKey})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current Tick", tickContext.CurrentTick.ToString());
            EditorGUILayout.LabelField("Partition Last Tick", tickContext.PartitionLastTick.ToString());
            EditorGUILayout.LabelField("Ticks Per Game Day", tickContext.TicksPerGameDay.ToString());
            EditorGUILayout.LabelField("Partition Tick Interval", tickContext.PartitionTickIntervalSeconds.ToString("0.###"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Regions", EditorStyles.boldLabel);
            foreach (RegionSnapshot region in starterWorldState.Regions)
            {
                EditorGUILayout.LabelField(
                    $"{region.Id}: {region.DisplayName}",
                    $"{region.Key} | {region.Type} | last tick {region.LastTick}"
                );
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Entities", EditorStyles.boldLabel);
            DrawEntityFilters();

            List<EntitySnapshot> filteredEntities = GetFilteredEntities().ToList();
            if (filteredEntities.Count == 0)
            {
                EditorGUILayout.HelpBox("No entities match the current filters.", MessageType.Info);
                selectedEntityIndex = -1;
                return;
            }

            string[] entityLabels = filteredEntities
                .Select(entity => $"{entity.Id}: {entity.DisplayName} ({entity.TemplateKey})")
                .ToArray();
            selectedEntityIndex = Mathf.Clamp(selectedEntityIndex, 0, entityLabels.Length - 1);
            selectedEntityIndex = EditorGUILayout.Popup("Selected Entity", selectedEntityIndex, entityLabels);

            EditorGUILayout.Space();
            DrawEntityDetails(filteredEntities[selectedEntityIndex]);
        }

        private void DrawDiagnosticsTab()
        {
            if (string.IsNullOrWhiteSpace(diagnostics))
            {
                EditorGUILayout.HelpBox("No diagnostics recorded.", MessageType.Info);
                return;
            }

            EditorGUILayout.TextArea(diagnostics, GUILayout.ExpandHeight(true));
        }

        private void DrawCatalogSummary()
        {
            if (catalogBuildResult == null)
            {
                EditorGUILayout.HelpBox("Catalog has not been built.", MessageType.Warning);
                return;
            }

            MessageType messageType = catalogBuildResult.IsValid ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(ContentCatalogValidationReport.FormatSummary(catalogBuildResult), messageType);

            foreach (ValidationMessage message in catalogBuildResult.ValidationResult.Messages)
            {
                EditorGUILayout.LabelField(ContentCatalogValidationReport.FormatMessage(message));
            }
        }

        private void DrawFactionCatalog(IReadOnlyDictionary<string, FactionDefinition> factions)
        {
            foreach (FactionDefinition faction in FilterDefinitions(factions.Values, faction => faction.key, faction => faction.displayName))
            {
                DrawFoldoutBox(
                    $"{faction.displayName} ({faction.key})",
                    () =>
                    {
                        DrawValue("Type", faction.type);
                        DrawValue("Parent", faction.parentKey);
                        DrawValue("Description", faction.description);
                    }
                );
            }
        }

        private void DrawRaceCatalog(IReadOnlyDictionary<string, RaceDefinition> races)
        {
            foreach (RaceDefinition race in FilterDefinitions(races.Values, race => race.key, race => race.displayName))
            {
                DrawFoldoutBox(
                    $"{race.displayName} ({race.key})",
                    () =>
                    {
                        DrawValue("Description", race.description);
                        DrawValue("Subraces", FormatList(race.subraces, subrace => $"{subrace.displayName} ({subrace.key})"));
                    }
                );
            }
        }

        private void DrawStatCatalog(IReadOnlyDictionary<string, StatDefinition> stats)
        {
            foreach (StatDefinition stat in FilterDefinitions(stats.Values, stat => stat.key, stat => stat.displayName))
            {
                DrawFoldoutBox(
                    $"{stat.displayName} ({stat.key})",
                    () => DrawValue("Description", stat.description)
                );
            }
        }

        private void DrawTraitCatalog(IReadOnlyDictionary<string, TraitDefinition> traits)
        {
            foreach (TraitDefinition trait in FilterDefinitions(traits.Values, trait => trait.key, trait => trait.displayName))
            {
                DrawFoldoutBox(
                    $"{trait.displayName} ({trait.key})",
                    () =>
                    {
                        DrawValue("Description", trait.description);
                        DrawValue("Requires", FormatList(trait.requiredTraitKeys));
                        DrawValue("Evolves To", trait.evolvesToKey);
                        DrawValue("Modifiers", FormatList(trait.modifiers, modifier => $"{modifier.statKey} {modifier.type} {modifier.value}"));
                    }
                );
            }
        }

        private void DrawRegionCatalog(IReadOnlyDictionary<string, RegionDefinition> regions)
        {
            foreach (RegionDefinition region in FilterDefinitions(regions.Values, region => region.key, region => region.displayName))
            {
                DrawFoldoutBox(
                    $"{region.displayName} ({region.key})",
                    () =>
                    {
                        DrawValue("Type", region.type);
                        DrawValue("Parent", region.parentKey);
                        DrawValue("Tick Interval", region.tickIntervalSeconds.ToString("0.###"));
                    }
                );
            }
        }

        private void DrawEntityTemplateCatalog(IReadOnlyDictionary<string, EntityTemplateDefinition> entityTemplates)
        {
            foreach (EntityTemplateDefinition template in FilterDefinitions(entityTemplates.Values, template => template.key, template => template.displayName))
            {
                DrawFoldoutBox(
                    $"{template.displayName} ({template.key})",
                    () =>
                    {
                        DrawValue("Race", JoinValues(template.raceKey, template.subraceKey));
                        DrawValue("Gender", template.gender);
                        DrawValue("House", template.houseKey);
                        DrawValue("Role", template.role);
                        DrawValue("Region", template.regionKey);
                        DrawValue("Age", template.age.ToString());
                        DrawValue("Traits", FormatList(template.traitKeys));
                        DrawValue("Magic", FormatList(template.magicKeys));
                        DrawValue("Factions", FormatList(template.factions, faction => $"{faction.factionKey} ({faction.rank})"));
                        DrawValue("Notes", template.notes);
                    }
                );
            }
        }

        private void DrawEntityFilters()
        {
            entitySearch = EditorGUILayout.TextField("Search", entitySearch);

            string[] regionOptions = new[] { "All Regions" }
                .Concat(starterWorldState.Regions.Select(region => $"{region.DisplayName} ({region.Id})"))
                .ToArray();
            selectedRegionFilter = Mathf.Clamp(selectedRegionFilter, 0, regionOptions.Length - 1);
            selectedRegionFilter = EditorGUILayout.Popup("Region", selectedRegionFilter, regionOptions);
            selectedAliveFilter = EditorGUILayout.Popup("Alive State", selectedAliveFilter, AliveFilters);
        }

        private void DrawEntityDetails(EntitySnapshot entity)
        {
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            DrawValue("Id", entity.Id.ToString());
            DrawValue("Name", entity.DisplayName);
            DrawValue("Template", entity.TemplateKey);
            DrawValue("Race", JoinValues(entity.RaceKey, entity.SubraceKey));
            DrawValue("Age Ticks", entity.AgeTicks.ToString());
            DrawValue("Alive", entity.IsAlive.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Position", EditorStyles.boldLabel);
            RegionSnapshot region = starterWorldState.Regions.FirstOrDefault(candidate => candidate.Id == entity.RegionId);
            DrawValue("Region", region == null ? entity.RegionId.ToString() : $"{region.DisplayName} ({region.Key})");
            DrawValue("Local Position", FormatPosition(entity));
            DrawValue("Heading Y", FormatNullable(entity.HeadingY));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Payload JSON", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.IsNullOrWhiteSpace(entity.PayloadJson) ? "{}" : entity.PayloadJson);
        }

        private void RefreshAll()
        {
            var builder = new StringBuilder();
            RefreshCatalog(builder);
            RefreshDatabaseStatus(builder);
            RefreshStarterWorld(builder);
            diagnostics = builder.ToString();
            Repaint();
        }

        private void RefreshCatalog()
        {
            var builder = new StringBuilder();
            RefreshCatalog(builder);
            diagnostics = builder.ToString();
            Repaint();
        }

        private void RefreshCatalog(StringBuilder builder)
        {
            try
            {
                catalogBuildResult = new ContentCatalogBuilder().Build();
                AppendDiagnostics(builder, "Catalog", ContentCatalogValidationReport.FormatSummary(catalogBuildResult));
            }
            catch (Exception exception)
            {
                catalogBuildResult = null;
                AppendDiagnostics(builder, "Catalog", exception);
            }
        }

        private void RefreshDatabaseStatus(StringBuilder builder)
        {
            try
            {
                databaseStatus = CreateDevelopmentInitializer().GetStatus();
                AppendDiagnostics(builder, "Database status", $"Loaded status for {databaseStatus.DatabasePath}");
            }
            catch (Exception exception)
            {
                databaseStatus = null;
                AppendDiagnostics(builder, "Database status", exception);
            }
        }

        private void RefreshStarterWorld(StringBuilder builder)
        {
            try
            {
                starterWorldState = LoadStarterWorldSnapshot();
                selectedEntityIndex = starterWorldState.Entities.Count > 0 ? 0 : -1;
                AppendDiagnostics(builder, "Starter world", $"Loaded {starterWorldState.Regions.Count} regions and {starterWorldState.Entities.Count} entities.");
            }
            catch (Exception exception)
            {
                starterWorldState = null;
                selectedEntityIndex = -1;
                AppendDiagnostics(builder, "Starter world", exception);
            }
        }

        private void GenerateStarterWorld()
        {
            var builder = new StringBuilder();
            try
            {
                ContentCatalogBuildResult result = catalogBuildResult ?? new ContentCatalogBuilder().Build();
                lastGenerationResult = CreateDevelopmentInitializer().GenerateStarterWorldIfEmpty(result);
                AppendDiagnostics(builder, "Generate starter world", FormatGenerationResult(lastGenerationResult));
            }
            catch (Exception exception)
            {
                lastGenerationResult = null;
                AppendDiagnostics(builder, "Generate starter world", exception);
            }

            RefreshCatalog(builder);
            RefreshDatabaseStatus(builder);
            RefreshStarterWorld(builder);
            diagnostics = builder.ToString();
            Repaint();
        }

        private void RebuildDevelopmentDatabase()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild Development Database",
                    "This will delete and recreate the Fantasy Sim development database.",
                    "Rebuild",
                    "Cancel"
                ))
            {
                return;
            }

            var builder = new StringBuilder();
            try
            {
                CreateDevelopmentInitializer().Rebuild();
                lastGenerationResult = null;
                AppendDiagnostics(builder, "Rebuild development database", $"Rebuilt database at {DatabasePath}");
            }
            catch (Exception exception)
            {
                AppendDiagnostics(builder, "Rebuild development database", exception);
            }

            RefreshDatabaseStatus(builder);
            RefreshStarterWorld(builder);
            diagnostics = builder.ToString();
            Repaint();
        }

        private IEnumerable<EntitySnapshot> GetFilteredEntities()
        {
            IEnumerable<EntitySnapshot> entities = starterWorldState.Entities;

            if (selectedRegionFilter > 0 && selectedRegionFilter <= starterWorldState.Regions.Count)
            {
                long regionId = starterWorldState.Regions[selectedRegionFilter - 1].Id;
                entities = entities.Where(entity => entity.RegionId == regionId);
            }

            if (selectedAliveFilter == 1)
            {
                entities = entities.Where(entity => entity.IsAlive);
            }
            else if (selectedAliveFilter == 2)
            {
                entities = entities.Where(entity => !entity.IsAlive);
            }

            if (!string.IsNullOrWhiteSpace(entitySearch))
            {
                entities = entities.Where(entity =>
                    Contains(entity.DisplayName, entitySearch) ||
                    Contains(entity.TemplateKey, entitySearch) ||
                    Contains(entity.RaceKey, entitySearch) ||
                    Contains(entity.SubraceKey, entitySearch)
                );
            }

            return entities;
        }

        private static LoadedPartitionSnapshot LoadStarterWorldSnapshot()
        {
            var repository = new StarterWorldReadRepository(DatabasePathProvider.ForEditorDevelopmentDatabase());
            MethodInfo loadMethod = typeof(StarterWorldReadRepository).GetMethod(nameof(StarterWorldReadRepository.LoadStarterWorld));
            if (loadMethod == null)
            {
                throw new InvalidOperationException("Starter world read method is unavailable.");
            }

            try
            {
                object loadedState = loadMethod.Invoke(repository, null);
                return CreateLoadedPartitionSnapshot(loadedState);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static LoadedPartitionSnapshot CreateLoadedPartitionSnapshot(object loadedState)
        {
            return new LoadedPartitionSnapshot
            {
                TickContext = CreateTickContextSnapshot(GetPropertyValue(loadedState, "TickContext")),
                Regions = GetEnumerableProperty(loadedState, "Regions")
                    .Cast<object>()
                    .Select(CreateRegionSnapshot)
                    .ToList(),
                Entities = GetEnumerableProperty(loadedState, "Entities")
                    .Cast<object>()
                    .Select(CreateEntitySnapshot)
                    .ToList()
            };
        }

        private static TickContextSnapshot CreateTickContextSnapshot(object tickContext)
        {
            return new TickContextSnapshot
            {
                CurrentTick = GetWrappedLong(tickContext, "CurrentTick"),
                TicksPerGameDay = GetInt(tickContext, "TicksPerGameDay"),
                PartitionId = GetLong(tickContext, "PartitionId"),
                PartitionKey = GetString(tickContext, "PartitionKey"),
                PartitionDisplayName = GetString(tickContext, "PartitionDisplayName"),
                PartitionTickIntervalSeconds = GetFloat(tickContext, "PartitionTickIntervalSeconds"),
                PartitionLastTick = GetWrappedLong(tickContext, "PartitionLastTick")
            };
        }

        private static RegionSnapshot CreateRegionSnapshot(object region)
        {
            return new RegionSnapshot
            {
                Id = GetWrappedLong(region, "Id"),
                Key = GetString(region, "Key"),
                DisplayName = GetString(region, "DisplayName"),
                Type = GetString(region, "Type"),
                ParentRegionId = GetNullableWrappedLong(region, "ParentRegionId"),
                TickIntervalSeconds = GetFloat(region, "TickIntervalSeconds"),
                LastTick = GetWrappedLong(region, "LastTick"),
                AuthorityMode = GetString(region, "AuthorityMode"),
                AuthorityOwner = GetString(region, "AuthorityOwner"),
                AuthorityUntilTick = GetNullableWrappedLong(region, "AuthorityUntilTick")
            };
        }

        private static EntitySnapshot CreateEntitySnapshot(object entity)
        {
            return new EntitySnapshot
            {
                Id = GetWrappedLong(entity, "Id"),
                TemplateKey = GetString(entity, "TemplateKey"),
                DisplayName = GetString(entity, "DisplayName"),
                RaceKey = GetString(entity, "RaceKey"),
                SubraceKey = GetString(entity, "SubraceKey"),
                AgeTicks = GetLong(entity, "AgeTicks"),
                IsAlive = GetBool(entity, "IsAlive"),
                PayloadJson = GetString(entity, "PayloadJson"),
                RegionId = GetWrappedLong(entity, "RegionId"),
                LocalX = GetNullableFloat(entity, "LocalX"),
                LocalY = GetNullableFloat(entity, "LocalY"),
                LocalZ = GetNullableFloat(entity, "LocalZ"),
                HeadingY = GetNullableFloat(entity, "HeadingY")
            };
        }

        private static IEnumerable GetEnumerableProperty(object source, string propertyName)
        {
            return (IEnumerable)GetPropertyValue(source, propertyName);
        }

        private static string GetString(object source, string propertyName)
        {
            return GetPropertyValue(source, propertyName) as string;
        }

        private static int GetInt(object source, string propertyName)
        {
            return Convert.ToInt32(GetPropertyValue(source, propertyName));
        }

        private static long GetLong(object source, string propertyName)
        {
            return Convert.ToInt64(GetPropertyValue(source, propertyName));
        }

        private static float GetFloat(object source, string propertyName)
        {
            return Convert.ToSingle(GetPropertyValue(source, propertyName));
        }

        private static float? GetNullableFloat(object source, string propertyName)
        {
            object value = GetPropertyValue(source, propertyName);
            return value == null ? (float?)null : Convert.ToSingle(value);
        }

        private static bool GetBool(object source, string propertyName)
        {
            return Convert.ToBoolean(GetPropertyValue(source, propertyName));
        }

        private static long GetWrappedLong(object source, string propertyName)
        {
            return GetLong(GetPropertyValue(source, propertyName), "Value");
        }

        private static long? GetNullableWrappedLong(object source, string propertyName)
        {
            object value = GetPropertyValue(source, propertyName);
            return value == null ? (long?)null : GetLong(value, "Value");
        }

        private static object GetPropertyValue(object source, string propertyName)
        {
            if (source == null)
            {
                throw new InvalidOperationException($"Cannot read '{propertyName}' from a null value.");
            }

            PropertyInfo property = source.GetType().GetProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Property '{propertyName}' is unavailable on {source.GetType().Name}.");
            }

            return property.GetValue(source, null);
        }

        private IEnumerable<T> FilterDefinitions<T>(
            IEnumerable<T> definitions,
            Func<T, string> keySelector,
            Func<T, string> displayNameSelector
        )
        {
            IEnumerable<T> filtered = definitions.OrderBy(keySelector);
            if (string.IsNullOrWhiteSpace(catalogSearch))
            {
                return filtered;
            }

            return filtered.Where(definition =>
                Contains(keySelector(definition), catalogSearch) ||
                Contains(displayNameSelector(definition), catalogSearch)
            );
        }

        private static void DrawFoldoutBox(string header, Action drawContents)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            drawContents();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private static void DrawValue(string label, string value)
        {
            EditorGUILayout.LabelField(label, string.IsNullOrWhiteSpace(value) ? "-" : value);
        }

        private static string FormatGenerationResult(StarterWorldGenerationResult result)
        {
            return $"{result.Message}\n" +
                   $"Regions inserted: {result.RegionsInserted}\n" +
                   $"Partitions inserted: {result.PartitionsInserted}\n" +
                   $"Entities inserted: {result.EntitiesInserted}";
        }

        private static string FormatPosition(EntitySnapshot entity)
        {
            return $"({FormatNullable(entity.LocalX)}, {FormatNullable(entity.LocalY)}, {FormatNullable(entity.LocalZ)})";
        }

        private static string FormatNullable(float? value)
        {
            return value.HasValue ? value.Value.ToString("0.###") : "-";
        }

        private static string FormatList(IEnumerable<string> values)
        {
            if (values == null)
            {
                return "-";
            }

            string joined = string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(joined) ? "-" : joined;
        }

        private static string FormatList<T>(IEnumerable<T> values, Func<T, string> formatter)
        {
            if (values == null)
            {
                return "-";
            }

            string joined = string.Join(", ", values.Select(formatter).Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(joined) ? "-" : joined;
        }

        private static string JoinValues(params string[] values)
        {
            return FormatList(values);
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AppendDiagnostics(StringBuilder builder, string section, string message)
        {
            builder.AppendLine($"[{section}]");
            builder.AppendLine(message);
            builder.AppendLine();
        }

        private static void AppendDiagnostics(StringBuilder builder, string section, Exception exception)
        {
            AppendDiagnostics(builder, section, exception.Message);
        }

        private static DatabaseInitializer CreateDevelopmentInitializer()
        {
            return new DatabaseInitializer(DatabasePathProvider.ForEditorDevelopmentDatabase());
        }

        private static string DatabasePath => DatabasePathProvider.ForEditorDevelopmentDatabase().DatabasePath;
    }
}
