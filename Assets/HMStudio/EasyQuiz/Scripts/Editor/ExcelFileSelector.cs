using UnityEditor;
using UnityEngine;
using System.IO;

namespace HMStudio.EasyQuiz
{
    public class ExcelFileSelector : EditorWindow
    {
        private string folderPath = "";

        [MenuItem("Tools/HMStudio/EasyQuiz/ImportFolder", priority = 0)]
        public static void ShowWindow()
        {
            GetWindow<ExcelFileSelector>("Select Excel Folder");
        }

        [MenuItem("Tools/HMStudio/EasyQuiz/Clear", priority = 1)]
        public static void ClearFolderPath()
        {
            PlayerPrefs.SetString("ExcelFolderPath", "");
        }

        private void OnEnable()
        {
            folderPath = PlayerPrefs.GetString("ExcelFolderPath", "");
        }

        private void OnGUI()
        {
            GUILayout.Label("Select an Excel Folder", EditorStyles.boldLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Folder:", folderPath, EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();

            if (GUILayout.Button("Browse", GUILayout.Height(30)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Excel Folder", Application.streamingAssetsPath, "");
                if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                {
                    folderPath = selectedPath;
                    PlayerPrefs.SetString("ExcelFolderPath", folderPath);
                }
            }

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(folderPath))
            {
                EditorGUILayout.HelpBox("Folder selected successfully!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("No folder selected.", MessageType.Warning);
            }
        }
    }
}