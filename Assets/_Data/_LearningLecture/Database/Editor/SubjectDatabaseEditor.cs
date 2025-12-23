using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using DreamClass.Subjects;
using System.Collections.Generic;

namespace DreamClass.Subjects.Editor
{
    [CustomEditor(typeof(SubjectDatabase))]
    public class SubjectDatabaseEditor : UnityEditor.Editor
    {
        private SerializedProperty subjectsProp;
        private ReorderableList reorderableList;
        private string searchText = "";
        
        // Cache properties to avoid finding them every frame
        private SerializedProperty csvFileProp;
        private SerializedProperty filePathProp;

        private void OnEnable()
        {
            subjectsProp = serializedObject.FindProperty("subjects");
            csvFileProp = serializedObject.FindProperty("csvFile");
            filePathProp = serializedObject.FindProperty("filePath");

            reorderableList = new ReorderableList(serializedObject, subjectsProp, true, true, true, true);

            reorderableList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, $"Subjects List ({subjectsProp.arraySize} items)");
            };

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                if (index >= subjectsProp.arraySize) return;

                var element = subjectsProp.GetArrayElementAtIndex(index);
                var nameProp = element.FindPropertyRelative("name");
                var gradeProp = element.FindPropertyRelative("grade");
                var cloudinaryProp = element.FindPropertyRelative("cloudinaryFolder");
                var titleProp = element.FindPropertyRelative("title");

                // Check filter
                if (!IsMatch(nameProp.stringValue, cloudinaryProp.stringValue, gradeProp.stringValue, titleProp.stringValue))
                {
                    // If filtered out, we try to hide it (height=0 is handled in elementHeightCallback)
                    return; 
                }

                rect.y += 2;
                
                // Calculate widths
                float nameWidth = rect.width * 0.4f;
                float gradeWidth = 60f;
                float cloudWidth = rect.width - nameWidth - gradeWidth - 10;

                // Name
                string displayName = string.IsNullOrEmpty(titleProp.stringValue) ? nameProp.stringValue : titleProp.stringValue;
                if (string.IsNullOrEmpty(displayName)) displayName = "[New Subject]";
                
                EditorGUI.LabelField(new Rect(rect.x, rect.y, nameWidth, EditorGUIUtility.singleLineHeight), displayName, EditorStyles.boldLabel);

                // Grade
                string gradeDisplay = string.IsNullOrEmpty(gradeProp.stringValue) ? "--" : gradeProp.stringValue;
                EditorGUI.LabelField(new Rect(rect.x + nameWidth + 5, rect.y, gradeWidth, EditorGUIUtility.singleLineHeight), $"Gr: {gradeDisplay}");

                // Cloudinary Status
                bool hasCloud = !string.IsNullOrEmpty(cloudinaryProp.stringValue);
                GUIStyle cloudStyle = new GUIStyle(EditorStyles.miniLabel);
                if (hasCloud) cloudStyle.normal.textColor = new Color(0, 0.5f, 0); // Dark Green
                else cloudStyle.normal.textColor = Color.red;
                
                string cloudText = hasCloud ? cloudinaryProp.stringValue : "[No Cloudinary]";
                EditorGUI.LabelField(new Rect(rect.x + nameWidth + gradeWidth + 10, rect.y, cloudWidth, EditorGUIUtility.singleLineHeight), cloudText, cloudStyle);
            };

            reorderableList.elementHeightCallback = (index) =>
            {
                 if (index >= subjectsProp.arraySize) return 0;
                 var element = subjectsProp.GetArrayElementAtIndex(index);
                 if (!IsMatch(element.FindPropertyRelative("name").stringValue, 
                              element.FindPropertyRelative("cloudinaryFolder").stringValue, 
                              element.FindPropertyRelative("grade").stringValue,
                              element.FindPropertyRelative("title").stringValue))
                 {
                     return 0; // Hide if filtered
                 }
                 return EditorGUIUtility.singleLineHeight + 4;
            };

            reorderableList.onSelectCallback = (ReorderableList l) =>
            {
                // Optional: Select behavior
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SubjectDatabase db = (SubjectDatabase)target;

            // --- CSV Section ---
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("CSV Source", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(csvFileProp);
            EditorGUILayout.PropertyField(filePathProp);
            
            if (GUILayout.Button("Load CSV As Subject"))
            {
                db.LoadCSVAsSubject();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // --- Search & List Section ---
            EditorGUILayout.LabelField("Subject Management", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                searchText = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            // Draw List
            reorderableList.DoLayoutList();

            // --- Selected Item Details ---
            if (reorderableList.index >= 0 && reorderableList.index < subjectsProp.arraySize)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Selected Subject Details", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                
                SerializedProperty selectedSubject = subjectsProp.GetArrayElementAtIndex(reorderableList.index);
                DrawSubjectDetails(selectedSubject);
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            
            if (GUILayout.Button("Log JSON"))
            {
                db.LogJson();
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Remote Data"))
            {
                if (EditorUtility.DisplayDialog("Confirm", "Clear all downloaded/remote data?", "Yes", "Cancel"))
                    db.ClearAllRemoteData();
            }
            if (GUILayout.Button("Clear All Sprites"))
            {
                db.ClearAllSprites();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSubjectDetails(SerializedProperty subject)
        {
            // Identity
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("name"));
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("description"));
            
            SerializedProperty cloudinary = subject.FindPropertyRelative("cloudinaryFolder");
            GUI.backgroundColor = string.IsNullOrEmpty(cloudinary.stringValue) ? new Color(1f, 0.8f, 0.8f) : Color.white;
            EditorGUILayout.PropertyField(cloudinary);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Metadata
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("grade"));
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("category"));
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("title"));
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("note"));

            EditorGUILayout.Space();

            // Lectures
            SerializedProperty lectures = subject.FindPropertyRelative("lectures");
            EditorGUILayout.PropertyField(lectures, new GUIContent($"Lectures ({lectures.arraySize})"), true); // Recursive draw for lectures list

            // Read-only info
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Info", EditorStyles.miniBoldLabel);
            GUI.enabled = false;
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("pages"));
            EditorGUILayout.PropertyField(subject.FindPropertyRelative("isCached"));
            GUI.enabled = true;
        }

        private bool IsMatch(string name, string cloudinary, string grade, string title)
        {
            if (string.IsNullOrEmpty(searchText)) return true;
            string search = searchText.ToLower();
            
            if (!string.IsNullOrEmpty(name) && name.ToLower().Contains(search)) return true;
            if (!string.IsNullOrEmpty(cloudinary) && cloudinary.ToLower().Contains(search)) return true;
            if (!string.IsNullOrEmpty(grade) && grade.ToLower().Contains(search)) return true;
            if (!string.IsNullOrEmpty(title) && title.ToLower().Contains(search)) return true;

            return false;
        }
    }
}