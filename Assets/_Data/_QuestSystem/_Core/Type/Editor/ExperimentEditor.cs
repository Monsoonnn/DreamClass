#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExperimentQuestStep))]
public class ExperimentQuestStepEditor : Editor
{
    private SerializedProperty stepIdProp;
    private SerializedProperty trackingModeProp;
    private SerializedProperty targetGuideStepIDProp;
    private SerializedProperty targetGuideStepIDsProp;
    private SerializedProperty completionRequirementProp;
    private SerializedProperty minCompletedCountProp;
    private SerializedProperty actionTypeProp;
    private SerializedProperty requireExperimentRunningProp;

    // Colors
    private Color headerColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);
    private Color warningColor = new Color(1f, 0.8f, 0.3f, 0.3f);

    private void OnEnable()
    {
        stepIdProp = serializedObject.FindProperty("StepId");
        trackingModeProp = serializedObject.FindProperty("trackingMode");
        targetGuideStepIDProp = serializedObject.FindProperty("targetGuideStepID");
        targetGuideStepIDsProp = serializedObject.FindProperty("targetGuideStepIDs");
        completionRequirementProp = serializedObject.FindProperty("completionRequirement");
        minCompletedCountProp = serializedObject.FindProperty("minCompletedCount");
        actionTypeProp = serializedObject.FindProperty("actionType");
        requireExperimentRunningProp = serializedObject.FindProperty("requireExperimentRunning");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ExperimentQuestStep step = (ExperimentQuestStep)target;

        // ===== BASIC INFO =====
        DrawHeader("Quest Step Info");
        EditorGUILayout.PropertyField(stepIdProp, new GUIContent("Step ID"));
        EditorGUILayout.Space(5);

        // ===== GUIDE STEP TRACKING =====
        DrawHeader("Guide Step Tracking");
        
        EditorGUILayout.PropertyField(trackingModeProp, new GUIContent("Tracking Mode"));
        
        ExperimentQuestStep.TrackingMode mode = (ExperimentQuestStep.TrackingMode)trackingModeProp.enumValueIndex;

        if (mode == ExperimentQuestStep.TrackingMode.Single)
        {
            DrawSingleMode();
        }
        else if(mode == ExperimentQuestStep.TrackingMode.Multiple)
        {
            DrawMultipleMode();
        }

        EditorGUILayout.Space(10);

        // ===== EXPERIMENT ACTION =====
        DrawHeader("Experiment Action");
        EditorGUILayout.PropertyField(actionTypeProp, new GUIContent("Action Type"));
        
        ExperimentQuestStep.ExperimentAction action = (ExperimentQuestStep.ExperimentAction)actionTypeProp.enumValueIndex;
        
        if (action != ExperimentQuestStep.ExperimentAction.None)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(requireExperimentRunningProp, new GUIContent("Require Running"));
            EditorGUI.indentLevel--;
            
            DrawActionHint(action);
        }

        EditorGUILayout.Space(10);

        // ===== RUNTIME STATUS (Play Mode Only) =====
        if (Application.isPlaying)
        {
            DrawRuntimeStatus(step);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSingleMode()
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(targetGuideStepIDProp, new GUIContent("Target Step ID"));
        
        if (string.IsNullOrEmpty(targetGuideStepIDProp.stringValue))
        {
            DrawWarningBox("⚠ No target step specified - this step will complete immediately!");
        }
        
        EditorGUI.indentLevel--;
    }

    private void DrawMultipleMode()
    {
        EditorGUI.indentLevel++;
        
        EditorGUILayout.PropertyField(targetGuideStepIDsProp, new GUIContent("Target Step IDs"), true);
        
        if (targetGuideStepIDsProp.arraySize == 0)
        {
            DrawWarningBox("⚠ No target steps specified - this step will complete immediately!");
        }
        else
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(completionRequirementProp, new GUIContent("Completion Rule"));
            
            ExperimentQuestStep.CompletionRequirement requirement = 
                (ExperimentQuestStep.CompletionRequirement)completionRequirementProp.enumValueIndex;

            if (requirement == ExperimentQuestStep.CompletionRequirement.Minimum)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(minCompletedCountProp, new GUIContent("Min Count"));
                
                int minCount = minCompletedCountProp.intValue;
                int totalCount = targetGuideStepIDsProp.arraySize;
                
                if (minCount <= 0)
                {
                    DrawWarningBox("⚠ Min count should be > 0");
                }
                else if (minCount > totalCount)
                {
                    DrawWarningBox($"⚠ Min count ({minCount}) exceeds total steps ({totalCount})");
                }
                else
                {
                    DrawInfoBox($"✓ Requires {minCount}/{totalCount} steps completed");
                }
                
                EditorGUI.indentLevel--;
            }
            else
            {
                DrawCompletionRuleHint(requirement, targetGuideStepIDsProp.arraySize);
            }
        }
        
        EditorGUI.indentLevel--;
    }

    private void DrawActionHint(ExperimentQuestStep.ExperimentAction action)
    {
        string hint = action switch
        {
            ExperimentQuestStep.ExperimentAction.SetupExperiment => "Will call experiment.SetupExperiment() when step starts",
            ExperimentQuestStep.ExperimentAction.StartExperiment => "Will call experiment.StartExperiment() when step starts",
            ExperimentQuestStep.ExperimentAction.StopExperiment => "Will call experiment.StopExperiment() when step starts",
            ExperimentQuestStep.ExperimentAction.WaitForCompletion => "Will wait until experiment stops running",
            
            _ => null
        };

        if (hint != null)
        {
            DrawInfoBox($"ℹ {hint}");
        }
    }

    private void DrawCompletionRuleHint(ExperimentQuestStep.CompletionRequirement requirement, int totalSteps)
    {
        string hint = requirement switch
        {
            ExperimentQuestStep.CompletionRequirement.All => 
                $"Requires ALL {totalSteps} steps completed",
            ExperimentQuestStep.CompletionRequirement.Any => 
                $"Requires ANY 1 of {totalSteps} steps completed",
            _ => null
        };

        if (hint != null)
        {
            DrawInfoBox($"✓ {hint}");
        }
    }

    private void DrawRuntimeStatus(ExperimentQuestStep step)
    {
        DrawHeader("Runtime Status", new Color(0.2f, 0.8f, 0.3f, 0.3f));
        
        EditorGUI.BeginDisabledGroup(true);
        
        EditorGUILayout.LabelField("Is Complete", step.IsComplete ? "✓ Yes" : "✗ No");
        
        if (!step.IsComplete)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Tracking Status:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            string status = "Disalled";
            //step.GetTrackingStatus();
            
            EditorGUILayout.TextArea(status, EditorStyles.helpBox);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("Force Complete (Debug)", GUILayout.Height(25)))
        {
            step.OnComplete();
            Debug.Log($"[Debug] Forced completion of step: {step.StepId}");
        }
    }

    private void DrawHeader(string title, Color? bgColor = null)
    {
        EditorGUILayout.Space(5);
        
        Color color = bgColor ?? headerColor;
        Rect rect = EditorGUILayout.GetControlRect(false, 25);
        EditorGUI.DrawRect(rect, color);
        
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        
        rect.x += 10;
        EditorGUI.LabelField(rect, title, style);
        
        EditorGUILayout.Space(5);
    }

    private void DrawInfoBox(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    private void DrawWarningBox(string message)
    {
        EditorGUILayout.HelpBox(message, MessageType.Warning);
    }
}
#endif