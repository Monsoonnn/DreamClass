using UnityEditor;
using UnityEngine;

namespace NPCCore.Animation {
    [CustomEditor(typeof(AnimationManager))]
    public class AnimationManagerEditor : Editor {
        // Editor-only parameter
        private bool editorEnableLoop = true;

        public override void OnInspectorGUI() {
            var manager = (AnimationManager)target;
            var so = new SerializedObject(manager);
            so.Update();

            // --- Draw default inspector (all serialized fields) ---
            DrawDefaultInspector();

            // --- Dropdown Group / Layer ---
            var animationSet = so.FindProperty("animationSet").objectReferenceValue as AnimationSetSO;
            var groupIndexProp = so.FindProperty("selectedGroupIndex");
            var layerIndexProp = so.FindProperty("selectedLayerIndex");

            if (animationSet != null && animationSet.groups.Count > 0) {
                // Group dropdown
                string[] groupNames = new string[animationSet.groups.Count];
                for (int i = 0; i < animationSet.groups.Count; i++)
                    groupNames[i] = animationSet.groups[i].groupName;

                groupIndexProp.intValue = EditorGUILayout.Popup("Group", groupIndexProp.intValue, groupNames);
                groupIndexProp.intValue = Mathf.Clamp(groupIndexProp.intValue, 0, animationSet.groups.Count - 1);

                var currentGroup = animationSet.GetGroupByIndex(groupIndexProp.intValue);

                if (currentGroup != null && currentGroup.layerAnimations.Count > 0) {
                    // Layer dropdown
                    string[] layerNames = new string[currentGroup.layerAnimations.Count];
                    for (int i = 0; i < currentGroup.layerAnimations.Count; i++) {
                        var entry = currentGroup.layerAnimations[i];
                        layerNames[i] = $"{entry.layer}: {entry.animationName}";
                    }

                    layerIndexProp.intValue = EditorGUILayout.Popup("Layer Animation", layerIndexProp.intValue, layerNames);
                    layerIndexProp.intValue = Mathf.Clamp(layerIndexProp.intValue, 0, currentGroup.layerAnimations.Count - 1);

                    GUILayout.Space(5);

                    // --- Parameter input ---
                    editorEnableLoop = EditorGUILayout.Toggle("Enable Loop", editorEnableLoop);

                    // --- Buttons ---
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Play Group"))
                        manager.PlaySelectedGroup();

                    /*if (GUILayout.Button("Play Layer"))
                        manager.PlaySelectedLayer(editorEnableLoop);*/
                    EditorGUILayout.EndHorizontal();
                } else {
                    EditorGUILayout.HelpBox("No layer animations found in this group.", MessageType.Info);
                }
            } else {
                EditorGUILayout.HelpBox("Assign an AnimationSetSO with groups.", MessageType.Info);
            }

            so.ApplyModifiedProperties();
        }
    }
}
