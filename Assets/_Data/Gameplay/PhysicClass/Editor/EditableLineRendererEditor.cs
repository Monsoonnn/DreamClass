using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EditableLineRenderer))]
public class EditableLineRendererEditor : Editor {
    void OnSceneGUI() {
        EditableLineRenderer editable = (EditableLineRenderer)target;
        if (editable.points == null || editable.points.Length == 0) return;

        for (int i = 0; i < editable.points.Length; i++) {
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(editable.points[i], Quaternion.identity);
            Handles.Label(editable.points[i] + Vector3.up * 0.05f, $"Point {i}");

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(editable, $"Move Point {i}");
                editable.points[i] = newPos;
                editable.lineRenderer.SetPosition(i, newPos);
                EditorUtility.SetDirty(editable);
            }
        }
    }
}
