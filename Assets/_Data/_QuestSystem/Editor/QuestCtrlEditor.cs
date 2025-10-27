using UnityEngine;
using UnityEditor;

namespace DreamClass.QuestSystem
{
    [CustomEditor(typeof(QuestCtrl))]
    public class QuestCtrlEditor : Editor
    {
        private bool showQuestInfo = true;
        private bool showSteps = true;
        private bool showRuntimeState = true;
        private bool showStepManagement = true;

        private QuestStep newStep;
        private GameObject stepFromScene;
        private int removeStepIndex = 0;

        public override void OnInspectorGUI()
        {
            QuestCtrl quest = (QuestCtrl)target;

            // Header
            EditorGUILayout.Space(5);
            DrawHeader(quest);
            EditorGUILayout.Space(10);

            // Quest Info
            showQuestInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showQuestInfo, "Quest Information");
            if (showQuestInfo) DrawQuestInfo(quest);
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(5);

            // Steps
            showSteps = EditorGUILayout.BeginFoldoutHeaderGroup(showSteps, "Quest Steps");
            if (showSteps) DrawSteps(quest);
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(5);

            // Step Management
            showStepManagement = EditorGUILayout.BeginFoldoutHeaderGroup(showStepManagement, "Step Management");
            if (showStepManagement) DrawStepManagement(quest);
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(5);

            // Runtime State
            showRuntimeState = EditorGUILayout.BeginFoldoutHeaderGroup(showRuntimeState, "Runtime State");
            if (showRuntimeState) DrawRuntimeState(quest);
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (GUI.changed)
                EditorUtility.SetDirty(quest);
        }

        private void DrawHeader(QuestCtrl quest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(quest.QuestName, new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            });
            if (!string.IsNullOrEmpty(quest.QuestId))
            {
                EditorGUILayout.LabelField($"ID: {quest.QuestId}", new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.gray }
                });
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawQuestInfo(QuestCtrl quest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            quest.QuestId = EditorGUILayout.TextField("Quest ID", quest.QuestId);
            quest.QuestName = EditorGUILayout.TextField("Quest Name", quest.QuestName);

            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            quest.Description = EditorGUILayout.TextArea(quest.Description, GUILayout.MinHeight(60));

            EditorGUILayout.LabelField("Quest State", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            Color currentColor = GetStateColor(quest.State);
            EditorGUILayout.LabelField($"Current: {quest.State}", new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = currentColor },
                fontStyle = FontStyle.Bold
            }, GUILayout.Width(150));
            quest.State = (QuestState)EditorGUILayout.EnumPopup(quest.State);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSteps(QuestCtrl quest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (quest.steps == null || quest.steps.Count == 0)
            {
                EditorGUILayout.HelpBox("No steps defined for this quest.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            int completed = 0;
            foreach (var step in quest.steps)
                if (step != null && step.IsComplete) completed++;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Steps: {quest.steps.Count}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Completed: {completed}/{quest.steps.Count}", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            float progress = quest.steps.Count > 0 ? (float)completed / quest.steps.Count : 0f;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), progress, $"{progress*100:F0}%");
            EditorGUILayout.Space(10);

            for (int i = 0; i < quest.steps.Count; i++)
                if (quest.steps[i] != null)
                    DrawStepItem(quest.steps[i], i);
                else
                    EditorGUILayout.HelpBox($"Step {i}: NULL", MessageType.Error);

            EditorGUILayout.EndVertical();
        }

        private void DrawStepItem(QuestStep step, int index)
        {
            Color bg = step.IsComplete ? new Color(0.6f, 1f, 0.6f, 0.3f) : new Color(1f, 1f, 1f, 0.1f);
            GUIStyle box = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10,10,5,5) };
            GUI.backgroundColor = bg;
            EditorGUILayout.BeginVertical(box);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"#{index+1}", GUILayout.Width(30));
            step.StepId = EditorGUILayout.TextField("ID", step.StepId);
            step.IsComplete = EditorGUILayout.Toggle(step.IsComplete, GUILayout.Width(20));

            string statusText = step.IsComplete ? "Complete" : "Pending";
            Color statusColor = step.IsComplete ? new Color(0.2f,0.8f,0.2f) : new Color(0.7f,0.7f,0.7f);
            EditorGUILayout.LabelField(statusText, new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = statusColor },
                fontStyle = FontStyle.Bold
            }, GUILayout.Width(70));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Type: {step.GetType().Name}", new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f,0.5f,0.5f) }
            });
            EditorGUILayout.EndVertical();
        }

        private void DrawStepManagement(QuestCtrl quest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Add Step từ Scene GameObject
            EditorGUILayout.LabelField("Add Step from Scene", EditorStyles.boldLabel);
            stepFromScene = (GameObject)EditorGUILayout.ObjectField("Step GameObject", stepFromScene, typeof(GameObject), true);
            GUI.enabled = stepFromScene != null;
            if (GUILayout.Button("Add Step", GUILayout.Height(25)))
            {
                if (quest.steps == null) quest.steps = new System.Collections.Generic.List<QuestStep>();
                QuestStep stepComp = stepFromScene.GetComponent<QuestStep>();
                if (stepComp != null)
                {
                    quest.steps.Add(stepComp);
                    EditorUtility.SetDirty(quest);
                    Debug.Log($"Added step '{stepComp.StepId}' from scene to quest '{quest.QuestName}'");
                }
                else
                {
                    Debug.LogWarning("Selected GameObject does not have a QuestStep component!");
                }
                stepFromScene = null;
            }
            GUI.enabled = true;
            EditorGUILayout.Space(10);

            // Remove Step
            if (quest.steps != null && quest.steps.Count > 0)
            {
                EditorGUILayout.LabelField("Remove Step", EditorStyles.boldLabel);
                string[] stepOptions = new string[quest.steps.Count];
                for (int i=0;i<quest.steps.Count;i++)
                    stepOptions[i] = quest.steps[i] != null ? $"#{i} - {quest.steps[i].StepId}" : $"#{i} - NULL";

                removeStepIndex = EditorGUILayout.Popup("Step Index", removeStepIndex, stepOptions);

                GUI.backgroundColor = new Color(1f,0.3f,0.3f);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("Remove Step", $"Are you sure you want to remove step at index {removeStepIndex}?", "Yes", "Cancel"))
                    {
                        quest.steps.RemoveAt(removeStepIndex);
                        if (removeStepIndex >= quest.steps.Count) removeStepIndex = quest.steps.Count-1;
                        EditorUtility.SetDirty(quest);
                        Debug.Log($"Removed step at index {removeStepIndex}");
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeState(QuestCtrl quest)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current State", GUILayout.Width(120));
            EditorGUILayout.LabelField(quest.State.ToString(), new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = GetStateColor(quest.State) }
            });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Is Complete", GUILayout.Width(120));
            EditorGUILayout.LabelField(quest.IsComplete ? "Yes" : "No", new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = quest.IsComplete ? Color.green : Color.red }
            });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                GUI.enabled = quest.State != QuestState.IN_PROGRESS && quest.State != QuestState.FINISHED;
                if (GUILayout.Button("Start Quest")) quest.StartQuest();
                GUI.enabled = true;

                GUI.enabled = quest.State == QuestState.IN_PROGRESS;
                if (GUILayout.Button("Complete Current Step"))
                {
                    if (quest.steps != null && quest.steps.Count > 0)
                    {
                        var currentStep = quest.steps.Find(s=>!s.IsComplete);
                        if (currentStep != null)
                        {
                            currentStep.IsComplete = true;
                            quest.UpdateProgress();
                        }
                    }
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Runtime controls available in Play Mode", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private Color GetStateColor(QuestState state)
        {
            return state switch
            {
                QuestState.NOT_PREMISE => Color.gray,
                QuestState.NOT_START => new Color(1f,0.8f,0f),
                QuestState.IN_PROGRESS => new Color(0.3f,0.7f,1f),
                QuestState.FINISHED => new Color(0.2f,0.8f,0.2f),
                _ => Color.white
            };
        }
    }
}
