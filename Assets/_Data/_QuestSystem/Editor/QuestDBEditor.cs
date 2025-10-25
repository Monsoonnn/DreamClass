using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DreamClass.Network;

namespace DreamClass.QuestSystem
{
    [CustomEditor(typeof(QuestDatabase))]
    public class QuestDatabaseEditor : Editor
    {
        private enum ViewMode
        {
            Overview,
            QuestList,
            Statistics
        }

        private ViewMode currentView = ViewMode.Overview;
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private QuestState filterState = QuestState.NOT_START;
        private bool useStateFilter = false;

        // Sync settings
        private enum SyncMode { LocalJSON, ServerAPI }
        private SyncMode syncMode = SyncMode.LocalJSON;
        private string jsonPath = "Assets/_Data/_QuestSystem/Mock/QuestMock.json";
        private ApiClient apiClient;
        private string questEndpoint = "/api/quests";

        private void OnEnable()
        {
            // Try to find ApiClient in scene
            apiClient = FindObjectOfType<ApiClient>();
        }

        public override void OnInspectorGUI()
        {
            QuestDatabase database = (QuestDatabase)target;

            DrawHeader(database);
            EditorGUILayout.Space(10);

            // View Mode Tabs
            DrawViewModeTabs();
            EditorGUILayout.Space(5);

            // Main content area
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (currentView)
            {
                case ViewMode.Overview:
                    DrawOverview(database);
                    break;
                case ViewMode.QuestList:
                    DrawQuestList(database);
                    break;
                case ViewMode.Statistics:
                    DrawStatistics(database);
                    break;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            DrawSyncTools(database);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(database);
            }
        }

        #region === HEADER ===

        private void DrawHeader(QuestDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("🗂️ Quest Database", titleStyle, GUILayout.Height(30));

            // Quick stats
            int totalQuests = database.questPrefabs != null ? database.questPrefabs.Count : 0;
            int nullCount = 0;
            if (database.questPrefabs != null)
            {
                foreach (var q in database.questPrefabs)
                {
                    if (q == null) nullCount++;
                }
            }

            GUIStyle statsStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            };
            EditorGUILayout.LabelField($"Total: {totalQuests} quests | Valid: {totalQuests - nullCount}", statsStyle);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region === VIEW MODE TABS ===

        private void DrawViewModeTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Toggle(currentView == ViewMode.Overview, "📊 Overview", "Button"))
                currentView = ViewMode.Overview;
            
            if (GUILayout.Toggle(currentView == ViewMode.QuestList, "📋 Quest List", "Button"))
                currentView = ViewMode.QuestList;
            
            if (GUILayout.Toggle(currentView == ViewMode.Statistics, "📈 Statistics", "Button"))
                currentView = ViewMode.Statistics;

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region === OVERVIEW VIEW ===

        private void DrawOverview(QuestDatabase database)
        {
            EditorGUILayout.LabelField("Database Overview", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (database.questPrefabs == null || database.questPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No quests in database. Add quest prefabs to get started.", MessageType.Info);
                return;
            }

            // Summary cards
            DrawSummaryCards(database);

            EditorGUILayout.Space(10);

            // Recent quests preview
            EditorGUILayout.LabelField("Recent Quests", EditorStyles.boldLabel);
            int previewCount = Mathf.Min(5, database.questPrefabs.Count);
            for (int i = 0; i < previewCount; i++)
            {
                var quest = database.questPrefabs[i];
                if (quest != null)
                {
                    DrawQuestPreviewCard(quest);
                }
            }

            if (database.questPrefabs.Count > 5)
            {
                EditorGUILayout.LabelField($"... and {database.questPrefabs.Count - 5} more", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawSummaryCards(QuestDatabase database)
        {
            EditorGUILayout.BeginHorizontal();

            // Count by state
            int locked = 0, notStarted = 0, inProgress = 0, finished = 0;
            foreach (var quest in database.questPrefabs)
            {
                if (quest == null) continue;
                switch (quest.State)
                {
                    case QuestState.NOT_PREMISE: locked++; break;
                    case QuestState.NOT_START: notStarted++; break;
                    case QuestState.IN_PROGRESS: inProgress++; break;
                    case QuestState.FINISHED: finished++; break;
                }
            }

            DrawStatCard("🔒 Locked", locked.ToString(), Color.gray);
            DrawStatCard("⭕ Not Started", notStarted.ToString(), new Color(1f, 0.8f, 0f));
            DrawStatCard("▶️ In Progress", inProgress.ToString(), new Color(0.3f, 0.7f, 1f));
            DrawStatCard("✅ Finished", finished.ToString(), new Color(0.2f, 0.8f, 0.2f));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatCard(string label, string value, Color color)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            };
            EditorGUILayout.LabelField(label, labelStyle);

            GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            };
            EditorGUILayout.LabelField(value, valueStyle, GUILayout.Height(30));

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestPreviewCard(QuestCtrl quest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // State icon
            string icon = GetStateIcon(quest.State);
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16
            };
            EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(30));

            // Quest info
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(quest.QuestName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {quest.QuestId} | Steps: {quest.steps?.Count ?? 0}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // Select button
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeObject = quest;
                EditorGUIUtility.PingObject(quest);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region === QUEST LIST VIEW ===

        private void DrawQuestList(QuestDatabase database)
        {
            EditorGUILayout.LabelField("Quest List", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Filters
            DrawFilters();
            EditorGUILayout.Space(5);

            if (database.questPrefabs == null || database.questPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No quests in database.", MessageType.Info);
                return;
            }

            // Quest list
            List<QuestCtrl> filteredQuests = GetFilteredQuests(database);

            EditorGUILayout.LabelField($"Showing {filteredQuests.Count} of {database.questPrefabs.Count} quests", EditorStyles.miniLabel);
            EditorGUILayout.Space(5);

            foreach (var quest in filteredQuests)
            {
                if (quest == null)
                {
                    DrawNullQuestCard();
                    continue;
                }

                DrawQuestDetailCard(quest);
            }
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search
            EditorGUILayout.LabelField("🔍", GUILayout.Width(20));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);

            GUILayout.Space(10);

            // State filter
            useStateFilter = EditorGUILayout.Toggle("Filter by State:", useStateFilter, GUILayout.Width(110));
            GUI.enabled = useStateFilter;
            filterState = (QuestState)EditorGUILayout.EnumPopup(filterState, EditorStyles.toolbarDropDown, GUILayout.Width(120));
            GUI.enabled = true;

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchFilter = "";
                useStateFilter = false;
            }

            EditorGUILayout.EndHorizontal();
        }

        private List<QuestCtrl> GetFilteredQuests(QuestDatabase database)
        {
            List<QuestCtrl> filtered = new List<QuestCtrl>();

            foreach (var quest in database.questPrefabs)
            {
                if (quest == null)
                {
                    filtered.Add(null);
                    continue;
                }

                // Search filter
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    bool matchName = quest.QuestName.ToLower().Contains(searchFilter.ToLower());
                    bool matchId = quest.QuestId.ToLower().Contains(searchFilter.ToLower());
                    if (!matchName && !matchId) continue;
                }

                // State filter
                if (useStateFilter && quest.State != filterState)
                    continue;

                filtered.Add(quest);
            }

            return filtered;
        }

        private void DrawNullQuestCard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚠️ NULL REFERENCE", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawQuestDetailCard(QuestCtrl quest)
        {
            Color bgColor = GetStateColor(quest.State);
            GUI.backgroundColor = new Color(bgColor.r, bgColor.g, bgColor.b, 0.3f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // Header
            EditorGUILayout.BeginHorizontal();
            
            string icon = GetStateIcon(quest.State);
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(30));

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(quest.QuestName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {quest.QuestId}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // State badge
            GUIStyle stateStyle = new GUIStyle(EditorStyles.miniButton)
            {
                normal = { textColor = GetStateColor(quest.State) },
                fontStyle = FontStyle.Bold
            };
            EditorGUILayout.LabelField(quest.State.ToString(), stateStyle, GUILayout.Width(100));

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeObject = quest;
                EditorGUIUtility.PingObject(quest);
            }

            EditorGUILayout.EndHorizontal();

            // Description
            if (!string.IsNullOrEmpty(quest.Description))
            {
                EditorGUILayout.LabelField(quest.Description, EditorStyles.wordWrappedMiniLabel);
            }

            // Steps info
            if (quest.steps != null && quest.steps.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"📝 Steps: {quest.steps.Count}", EditorStyles.miniLabel, GUILayout.Width(100));

                int completedSteps = 0;
                foreach (var step in quest.steps)
                {
                    if (step != null && step.IsComplete) completedSteps++;
                }

                float progress = (float)completedSteps / quest.steps.Count;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(16)), progress, $"{completedSteps}/{quest.steps.Count}");

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        #endregion

        #region === STATISTICS VIEW ===

        private void DrawStatistics(QuestDatabase database)
        {
            EditorGUILayout.LabelField("Database Statistics", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (database.questPrefabs == null || database.questPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No quests in database.", MessageType.Info);
                return;
            }

            // Calculate stats
            int total = database.questPrefabs.Count;
            int nullCount = 0;
            int totalSteps = 0;
            int completedSteps = 0;
            int questsWithSteps = 0;
            Dictionary<QuestState, int> stateCounts = new Dictionary<QuestState, int>();

            foreach (var quest in database.questPrefabs)
            {
                if (quest == null)
                {
                    nullCount++;
                    continue;
                }

                // State count
                if (!stateCounts.ContainsKey(quest.State))
                    stateCounts[quest.State] = 0;
                stateCounts[quest.State]++;

                // Steps count
                if (quest.steps != null && quest.steps.Count > 0)
                {
                    questsWithSteps++;
                    totalSteps += quest.steps.Count;
                    foreach (var step in quest.steps)
                    {
                        if (step != null && step.IsComplete)
                            completedSteps++;
                    }
                }
            }

            // Draw stats sections
            DrawGeneralStats(total, nullCount);
            EditorGUILayout.Space(10);

            DrawStateDistribution(stateCounts, total - nullCount);
            EditorGUILayout.Space(10);

            DrawStepStats(totalSteps, completedSteps, questsWithSteps);
        }

        private void DrawGeneralStats(int total, int nullCount)
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatRow("Total Quests:", total.ToString());
            DrawStatRow("Valid Quests:", (total - nullCount).ToString(), Color.green);
            if (nullCount > 0)
                DrawStatRow("Null References:", nullCount.ToString(), Color.red);

            EditorGUILayout.EndVertical();
        }

        private void DrawStateDistribution(Dictionary<QuestState, int> stateCounts, int validTotal)
        {
            EditorGUILayout.LabelField("State Distribution", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (QuestState state in System.Enum.GetValues(typeof(QuestState)))
            {
                int count = stateCounts.ContainsKey(state) ? stateCounts[state] : 0;
                float percentage = validTotal > 0 ? (float)count / validTotal * 100f : 0f;
                
                EditorGUILayout.BeginHorizontal();
                string icon = GetStateIcon(state);
                EditorGUILayout.LabelField($"{icon} {state}:", GUILayout.Width(150));
                EditorGUILayout.LabelField(count.ToString(), EditorStyles.boldLabel, GUILayout.Width(40));
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(16)), percentage / 100f, $"{percentage:F1}%");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStepStats(int totalSteps, int completedSteps, int questsWithSteps)
        {
            EditorGUILayout.LabelField("Steps Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatRow("Quests with Steps:", questsWithSteps.ToString());
            DrawStatRow("Total Steps:", totalSteps.ToString());
            DrawStatRow("Completed Steps:", completedSteps.ToString(), Color.green);
            DrawStatRow("Pending Steps:", (totalSteps - completedSteps).ToString(), new Color(1f, 0.8f, 0f));

            if (totalSteps > 0)
            {
                float completion = (float)completedSteps / totalSteps * 100f;
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Overall Completion:");
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), completion / 100f, $"{completion:F1}%");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatRow(string label, string value, Color? color = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            
            GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel);
            if (color.HasValue)
                valueStyle.normal.textColor = color.Value;
            
            EditorGUILayout.LabelField(value, valueStyle);
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region === SYNC TOOLS ===

        private void DrawSyncTools(QuestDatabase database)
        {
            // Quest Management Section
            EditorGUILayout.LabelField("Quest Management", EditorStyles.boldLabel);
            DrawQuestManagement(database);
            
            EditorGUILayout.Space(15);
            
            // Sync Tools Section
            EditorGUILayout.LabelField("Sync Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("⚠️ This tool syncs quest metadata into prefabs (Editor only). Runtime state is managed by QuestManager.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Sync mode selection
            syncMode = (SyncMode)EditorGUILayout.EnumPopup("Sync Mode:", syncMode);
            EditorGUILayout.Space(5);

            if (syncMode == SyncMode.LocalJSON)
            {
                DrawLocalJSONSync(database);
            }
            else
            {
                DrawServerAPISync(database);
            }

            EditorGUILayout.Space(10);
            DrawUtilityButtons(database);
        }
        
        private QuestCtrl questToAdd;
        private string questIdToRemove = "";
        
        private void DrawQuestManagement(QuestDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Add Quest Section
            EditorGUILayout.LabelField("➕ Add Quest", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            questToAdd = (QuestCtrl)EditorGUILayout.ObjectField("Quest Prefab:", questToAdd, typeof(QuestCtrl), false);
            
            GUI.enabled = questToAdd != null;
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("Add", GUILayout.Width(60), GUILayout.Height(20)))
            {
                database.AddQuest(questToAdd);
                questToAdd = null;
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Remove Quest Section
            EditorGUILayout.LabelField("➖ Remove Quest", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quest ID:", GUILayout.Width(70));
            questIdToRemove = EditorGUILayout.TextField(questIdToRemove);
            
            GUI.enabled = !string.IsNullOrEmpty(questIdToRemove);
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button("Remove", GUILayout.Width(70), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("Remove Quest", 
                    $"Are you sure you want to remove quest '{questIdToRemove}'?", 
                    "Yes, Remove", "Cancel"))
                {
                    database.RemoveQuest(questIdToRemove);
                    questIdToRemove = "";
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Quick add from selection
            if (Selection.activeObject is QuestCtrl selectedQuest)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox($"Selected: {selectedQuest.QuestName}", MessageType.Info);
                if (GUILayout.Button("Quick Add Selected", GUILayout.Width(130)))
                {
                    database.AddQuest(selectedQuest);
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawLocalJSONSync(QuestDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 Local JSON Mock", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("JSON Path:", GUILayout.Width(80));
            jsonPath = EditorGUILayout.TextField(jsonPath);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("📂 Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("Select Quest JSON", "Assets", "json");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    jsonPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }

            EditorGUILayout.Space(5);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("📥 Load & Sync from Local JSON", GUILayout.Height(35)))
            {
                LoadAndSyncFromJSON(database);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawServerAPISync(QuestDatabase database)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🌐 Server API", EditorStyles.boldLabel);

            // ApiClient field
            apiClient = (ApiClient)EditorGUILayout.ObjectField("API Client:", apiClient, typeof(ApiClient), true);
            
            if (apiClient == null)
            {
                EditorGUILayout.HelpBox("ApiClient not found. Please assign or add ApiClient to scene.", MessageType.Warning);
                if (GUILayout.Button("Find ApiClient in Scene"))
                {
                    apiClient = FindObjectOfType<ApiClient>();
                    if (apiClient == null)
                    {
                        EditorUtility.DisplayDialog("Not Found", "No ApiClient found in scene.", "OK");
                    }
                }
            }

            // Endpoint
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Endpoint:", GUILayout.Width(80));
            questEndpoint = EditorGUILayout.TextField(questEndpoint);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            GUI.enabled = apiClient != null;
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("🌐 Fetch & Sync from Server", GUILayout.Height(35)))
            {
                FetchAndSyncFromServer(database);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
        }

        private void DrawUtilityButtons(QuestDatabase database)
        {
            EditorGUILayout.BeginHorizontal();

            // Validate
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("🔍 Validate Prefabs", GUILayout.Height(30)))
            {
                ValidatePrefabs(database);
            }

            // Clear
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("🗑️ Clear All", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear All Quests", "Are you sure?", "Yes", "Cancel"))
                {
                    database.ClearAll();
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region === SYNC LOGIC ===

        private void LoadAndSyncFromJSON(QuestDatabase database)
        {
            if (!File.Exists(jsonPath))
            {
                EditorUtility.DisplayDialog("Error", $"JSON file not found:\n{jsonPath}", "OK");
                return;
            }

            try
            {
                string json = File.ReadAllText(jsonPath);
                PlayerQuestJson playerData = JsonUtility.FromJson<PlayerQuestJson>(json);
                SyncQuestData(database, playerData);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to load JSON:\n{e.Message}", "OK");
                Debug.LogError($"[Editor] Sync error: {e}");
            }
        }

        private void FetchAndSyncFromServer(QuestDatabase database)
        {
            if (apiClient == null)
            {
                EditorUtility.DisplayDialog("Error", "ApiClient not assigned", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Fetching from Server", "Preparing request...", 0.1f);

            ApiRequest request = new ApiRequest(questEndpoint, "GET");

            apiClient.StartCoroutine(apiClient.SendRequest(request, response =>
            {
                EditorUtility.ClearProgressBar();

                if (response.IsSuccess)
                {
                    try
                    {
                        PlayerQuestJson playerData = JsonUtility.FromJson<PlayerQuestJson>(response.Text);
                        SyncQuestData(database, playerData);
                    }
                    catch (System.Exception e)
                    {
                        EditorUtility.DisplayDialog("Error", $"Failed to parse response:\n{e.Message}", "OK");
                        Debug.LogError($"[Editor] Parse error: {e}");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", $"Request failed:\n{response.Error}", "OK");
                    Debug.LogError($"[Editor] Request error: {response.Error}");
                }
            }));
        }

        private void SyncQuestData(QuestDatabase database, PlayerQuestJson playerData)
        {
            if (playerData == null || playerData.quests == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to parse quest data", "OK");
                return;
            }

            int syncedCount = 0;
            int notFoundCount = 0;
            string notFoundQuests = "";

            foreach (var questData in playerData.quests)
            {
                QuestCtrl prefab = database.GetQuestPrefabById(questData.questId);

                if (prefab == null)
                {
                    notFoundCount++;
                    notFoundQuests += $"\n• {questData.questId} - {questData.name}";
                    continue;
                }

                bool changed = false;

                // Sync metadata
                if (prefab.QuestName != questData.name)
                {
                    prefab.QuestName = questData.name;
                    changed = true;
                }

                if (prefab.Description != questData.description)
                {
                    prefab.Description = questData.description;
                    changed = true;
                }

                // Sync state
                if (System.Enum.TryParse(questData.state, out QuestState newState))
                {
                    if (prefab.State != newState)
                    {
                        prefab.State = newState;
                        changed = true;
                    }
                }

                // Sync steps
                if (questData.steps != null && prefab.steps != null)
                {
                    foreach (var stepData in questData.steps)
                    {
                        var step = prefab.steps.Find(s => s.StepId == stepData.stepId);
                        if (step != null && step.IsComplete != stepData.isComplete)
                        {
                            step.IsComplete = stepData.isComplete;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(prefab);
                    syncedCount++;
                    Debug.Log($"<color=green>[Editor] Synced quest: {questData.questId}</color>");
                }
            }

            // Save changes
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Show result
            string message = $"✅ Synced: {syncedCount} quest(s)\n";
            if (notFoundCount > 0)
            {
                message += $"\n⚠️ Not Found: {notFoundCount} quest(s){notFoundQuests}";
            }

            EditorUtility.DisplayDialog("Sync Complete", message, "OK");
            Debug.Log($"<color=cyan>[Editor] Sync completed: {syncedCount} synced, {notFoundCount} not found</color>");
        }

        private void ValidatePrefabs(QuestDatabase database)
        {
            if (database.questPrefabs == null || database.questPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation", "No quests in database", "OK");
                return;
            }

            int nullCount = 0;
            int noIdCount = 0;
            int noStepsCount = 0;
            string issues = "";

            foreach (var quest in database.questPrefabs)
            {
                if (quest == null)
                {
                    nullCount++;
                    continue;
                }

                if (string.IsNullOrEmpty(quest.QuestId))
                {
                    noIdCount++;
                    issues += $"\n• {quest.name} - Missing QuestId";
                }

                if (quest.steps == null || quest.steps.Count == 0)
                {
                    noStepsCount++;
                    issues += $"\n• {quest.QuestId} - No steps defined";
                }
            }

            string message = $"Total Quests: {database.questPrefabs.Count}\n";
            message += $"✅ Valid: {database.questPrefabs.Count - nullCount - noIdCount}\n";
            
            if (nullCount > 0) message += $"⚠️ Null References: {nullCount}\n";
            if (noIdCount > 0) message += $"⚠️ Missing QuestId: {noIdCount}\n";
            if (noStepsCount > 0) message += $"⚠️ No Steps: {noStepsCount}\n";
            
            if (!string.IsNullOrEmpty(issues))
            {
                message += $"\nIssues:{issues}";
            }

            EditorUtility.DisplayDialog("Validation Report", message, "OK");
        }

        #endregion

        #region === HELPER METHODS ===

        private string GetStateIcon(QuestState state)
        {
            return state switch
            {
                QuestState.NOT_PREMISE => "🔒",
                QuestState.NOT_START => "⭕",
                QuestState.IN_PROGRESS => "▶️",
                QuestState.FINISHED => "✅",
                _ => "❓"
            };
        }

        private Color GetStateColor(QuestState state)
        {
            return state switch
            {
                QuestState.NOT_PREMISE => Color.gray,
                QuestState.NOT_START => new Color(1f, 0.8f, 0f),
                QuestState.IN_PROGRESS => new Color(0.3f, 0.7f, 1f),
                QuestState.FINISHED => new Color(0.2f, 0.8f, 0.2f),
                _ => Color.white
            };
        }

        #endregion
    }
}