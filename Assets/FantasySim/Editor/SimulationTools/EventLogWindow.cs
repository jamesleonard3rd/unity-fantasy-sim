using System.Collections.Generic;
using FantasySim.Core.Ids;
using FantasySim.Core.Time;
using FantasySim.Persistence.SQLite;
using FantasySim.Session;
using UnityEditor;
using UnityEngine;

namespace FantasySim.Editor.SimulationTools
{
    public sealed class EventLogWindow : EditorWindow
    {
        private long regionIdFilter;
        private long entityIdFilter;
        private string typeFilter = string.Empty;
        private int minSignificance;
        private bool useTickRange;
        private long minTick;
        private long maxTick;
        private int limit = WorldEventReadRepository.DefaultLimit;

        private Vector2 scrollPosition;
        private IReadOnlyList<WorldEventRecord> results = new List<WorldEventRecord>();
        private RegionEntrySummary summary;
        private string statusMessage = "Press Refresh to query the development database.";

        [MenuItem("Fantasy Sim/Simulation/Event Log Viewer")]
        public static void Open()
        {
            EventLogWindow window = GetWindow<EventLogWindow>();
            window.titleContent = new GUIContent("Event Log");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            regionIdFilter = EditorGUILayout.LongField("Region Id (0 = any)", regionIdFilter);
            entityIdFilter = EditorGUILayout.LongField("Entity Id (0 = any)", entityIdFilter);
            typeFilter = EditorGUILayout.TextField("Type (blank = any)", typeFilter);
            minSignificance = EditorGUILayout.IntField("Min Significance", minSignificance);

            useTickRange = EditorGUILayout.Toggle("Use Tick Range", useTickRange);
            using (new EditorGUI.DisabledScope(!useTickRange))
            {
                minTick = EditorGUILayout.LongField("Min Tick", minTick);
                maxTick = EditorGUILayout.LongField("Max Tick", maxTick);
            }

            limit = Mathf.Max(1, EditorGUILayout.IntField("Limit", limit));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                {
                    Refresh();
                }

                if (GUILayout.Button("Clear Filters"))
                {
                    ClearFilters();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

            if (summary != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Region Entry Summary", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(summary.ToDisplayText(), MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Events ({results.Count})", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (WorldEventRecord record in results)
            {
                DrawEventRow(record);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEventRow(WorldEventRecord record)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string region = record.RegionId.HasValue ? record.RegionId.Value.Value.ToString() : "-";
                string subject = record.SubjectEntityId.HasValue ? record.SubjectEntityId.Value.Value.ToString() : "-";
                string target = record.TargetEntityId.HasValue ? record.TargetEntityId.Value.Value.ToString() : "-";

                EditorGUILayout.LabelField(
                    $"tick {record.OccurredTick.Value}  |  {record.Type}  |  sig {record.Significance}"
                );
                EditorGUILayout.LabelField($"region {region}  |  subject {subject}  |  target {target}");
                if (!string.IsNullOrEmpty(record.PayloadJson))
                {
                    EditorGUILayout.LabelField(record.PayloadJson, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void Refresh()
        {
            DatabasePathProvider pathProvider = DatabasePathProvider.ForEditorDevelopmentDatabase();
            var repository = new WorldEventReadRepository(pathProvider);

            var query = new WorldEventQuery
            {
                RegionId = regionIdFilter > 0 ? new RegionId(regionIdFilter) : (RegionId?)null,
                EntityId = entityIdFilter > 0 ? new EntityId(entityIdFilter) : (EntityId?)null,
                Type = string.IsNullOrWhiteSpace(typeFilter) ? null : typeFilter.Trim(),
                MinSignificance = minSignificance > 0 ? minSignificance : (int?)null,
                MinTick = useTickRange ? new GameTick(minTick) : (GameTick?)null,
                MaxTick = useTickRange ? new GameTick(maxTick) : (GameTick?)null,
                Limit = limit
            };

            try
            {
                results = repository.Query(query);
                summary = new RegionEntrySummaryService().Build(results);
                statusMessage = $"Loaded {results.Count} event(s) from {pathProvider.DatabasePath}.";
            }
            catch (System.Exception exception)
            {
                results = new List<WorldEventRecord>();
                summary = null;
                statusMessage = $"Query failed: {exception.Message}";
            }
        }

        private void ClearFilters()
        {
            regionIdFilter = 0;
            entityIdFilter = 0;
            typeFilter = string.Empty;
            minSignificance = 0;
            useTickRange = false;
            minTick = 0;
            maxTick = 0;
            limit = WorldEventReadRepository.DefaultLimit;
        }
    }
}
