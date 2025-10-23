using UnityEditor;
using UnityEngine;
using Systems.SceneManagement;

[CustomEditor(typeof(ChangeClass))]
public class ChangeClassEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        var changeClass = (ChangeClass)target;
        SceneLoader loader = changeClass.SceneLoader != null
            ? changeClass.SceneLoader
            : GameObject.FindAnyObjectByType<SceneLoader>();

        if (loader == null || loader.GetSceneGroups() == null || loader.GetSceneGroups().Length == 0) {
            EditorGUILayout.HelpBox("No SceneLoader or SceneGroups found.", MessageType.Warning);
            return;
        }

        string[] groupNames = new string[loader.GetSceneGroups().Length];
        for (int i = 0; i < groupNames.Length; i++)
            groupNames[i] = loader.GetSceneGroups()[i].GroupName;

        int currentIndex = Mathf.Max(0, System.Array.IndexOf(groupNames, changeClass.GroupName));
        int selectedIndex = EditorGUILayout.Popup("Scene Group", currentIndex, groupNames);

        if (selectedIndex >= 0 && selectedIndex < groupNames.Length) {
            string selectedName = groupNames[selectedIndex];
            if (changeClass.GroupName != selectedName) {
                Undo.RecordObject(changeClass, "Change Scene Group");
                changeClass.GroupName = selectedName;
                EditorUtility.SetDirty(changeClass);
            }
        }
    }
}
