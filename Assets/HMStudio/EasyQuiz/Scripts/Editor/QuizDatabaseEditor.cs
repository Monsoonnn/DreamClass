using UnityEngine;
using UnityEditor;

namespace HMStudio.EasyQuiz.Editor
{
    /// <summary>
    /// Custom Editor cho QuizDatabase - hỗ trợ hiển thị 2 mode Excel và API
    /// </summary>
    [CustomEditor(typeof(QuizDatabase))]
    public class QuizDatabaseEditor : UnityEditor.Editor
    {
        private SerializedProperty dataModeProperty;
        private SerializedProperty apiBaseURLProperty;
        private SerializedProperty apiEndpointProperty;
        private SerializedProperty excelSubjectsProperty;
        private SerializedProperty apiSubjectsProperty;

        private bool showExcelData = true;
        private bool showAPIData = true;
        private bool showAPIPreview = false;
        private string testFetchResult = "";

        private void OnEnable()
        {
            dataModeProperty = serializedObject.FindProperty("dataMode");
            apiBaseURLProperty = serializedObject.FindProperty("apiBaseURL");
            apiEndpointProperty = serializedObject.FindProperty("apiEndpoint");
            excelSubjectsProperty = serializedObject.FindProperty("excelSubjects");
            apiSubjectsProperty = serializedObject.FindProperty("apiSubjects");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // === Mode Selection ===
            EditorGUILayout.LabelField("⚙️ Chế độ nguồn dữ liệu", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dataModeProperty, new GUIContent("Data Mode"));
            
            QuizDataMode currentMode = (QuizDataMode)dataModeProperty.enumValueIndex;
            
            // Hiển thị mode đang active
            string modeText = currentMode == QuizDataMode.Excel ? "📊 EXCEL MODE ACTIVE" : "🌐 API MODE ACTIVE";
            Color modeColor = currentMode == QuizDataMode.Excel ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.2f, 0.4f, 0.8f);
            
            GUI.backgroundColor = modeColor;
            EditorGUILayout.HelpBox(modeText, MessageType.None);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(15);

            // === EXCEL DATA SECTION ===
            DrawExcelSection(currentMode == QuizDataMode.Excel);

            EditorGUILayout.Space(15);

            // === API DATA SECTION ===
            DrawAPISection(currentMode == QuizDataMode.API);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawExcelSection(bool isActive)
        {
            // Header với màu
            GUI.backgroundColor = isActive ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            showExcelData = EditorGUILayout.Foldout(showExcelData, "", true);
            EditorGUILayout.LabelField("📊 EXCEL DATA" + (isActive ? " (ACTIVE)" : " (Inactive)"), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (showExcelData)
            {
                if (!isActive)
                {
                    EditorGUILayout.HelpBox(
                        "Mode Excel không active. Chuyển Data Mode sang 'Excel' để sử dụng.",
                        MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Dữ liệu câu hỏi được đọc từ file Excel local.\n" +
                        "Mỗi Subject có nhiều Chapter, mỗi Chapter chỉ định path tới file Excel.",
                        MessageType.Info);
                }

                EditorGUILayout.Space(5);
                
                // Disable nếu không active
                GUI.enabled = isActive;
                EditorGUILayout.PropertyField(excelSubjectsProperty, new GUIContent("Excel Subjects"), true);
                GUI.enabled = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAPISection(bool isActive)
        {
            // Header với màu
            GUI.backgroundColor = isActive ? new Color(0.3f, 0.5f, 1f) : new Color(0.5f, 0.5f, 0.5f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            showAPIData = EditorGUILayout.Foldout(showAPIData, "", true);
            EditorGUILayout.LabelField("🌐 API DATA" + (isActive ? " (ACTIVE)" : " (Inactive)"), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (showAPIData)
            {
                if (!isActive)
                {
                    EditorGUILayout.HelpBox(
                        "Mode API không active. Chuyển Data Mode sang 'API' để sử dụng.",
                        MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Dữ liệu câu hỏi được fetch từ server API.\n" +
                        "Cấu hình URL và endpoint bên dưới, sau đó nhấn 'Fetch API Data' để tải.",
                        MessageType.Info);
                }

                EditorGUILayout.Space(5);

                // API Configuration - luôn hiển thị
                EditorGUILayout.LabelField("Cấu hình API", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(apiBaseURLProperty, new GUIContent("Base URL"));
                EditorGUILayout.PropertyField(apiEndpointProperty, new GUIContent("Endpoint"));

                EditorGUILayout.Space(10);

                // Buttons
                GUI.enabled = isActive;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔄 Fetch API Data", GUILayout.Height(30)))
                {
                    FetchAPIData();
                }
                if (GUILayout.Button("🗑️ Clear Cache", GUILayout.Height(30)))
                {
                    ClearAPICache();
                }
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;

                // Fetch result
                if (!string.IsNullOrEmpty(testFetchResult))
                {
                    MessageType msgType = testFetchResult.Contains("✓") ? MessageType.Info : 
                                          testFetchResult.Contains("✗") ? MessageType.Error : MessageType.None;
                    EditorGUILayout.HelpBox(testFetchResult, msgType);
                }

                EditorGUILayout.Space(10);

                // Cached API Data (Read-only)
                EditorGUILayout.LabelField("Cached API Subjects (Read-only)", EditorStyles.miniBoldLabel);
                GUI.enabled = false;
                EditorGUILayout.PropertyField(apiSubjectsProperty, new GUIContent("API Subjects"), true);
                GUI.enabled = true;

                // Preview từ Service
                EditorGUILayout.Space(5);
                showAPIPreview = EditorGUILayout.Foldout(showAPIPreview, "📋 Live Preview (từ Service)", true);
                if (showAPIPreview)
                {
                    DrawAPIPreview();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void FetchAPIData()
        {
            testFetchResult = "⏳ Đang tải...";
            
            var quizDB = (QuizDatabase)target;
            
            // Tạo QuizAPIService nếu chưa có
            if (QuizAPIService.Instance == null)
            {
                var go = new GameObject("QuizAPIService_Editor");
                go.AddComponent<QuizAPIService>();
            }

            QuizAPIService.Instance.Configure(quizDB.APIBaseURL, quizDB.APIEndpoint);
            QuizAPIService.Instance.ClearCache();
            
            EditorApplication.update += CheckFetchResult;
            QuizAPIService.Instance.FetchQuizzes((success, message) =>
            {
                if (success)
                {
                    // Sync to local cache
                    quizDB.SyncAPIDataToLocal();
                    var subjects = QuizAPIService.Instance.GetCachedSubjects();
                    testFetchResult = $"✓ Thành công! Đã tải {subjects.Count} subjects.";
                }
                else
                {
                    testFetchResult = $"✗ Lỗi: {message}";
                }
                EditorApplication.update -= CheckFetchResult;
                Repaint();
            });
        }

        private void ClearAPICache()
        {
            var quizDB = (QuizDatabase)target;
            
            if (QuizAPIService.Instance != null)
            {
                QuizAPIService.Instance.ClearCache();
            }
            
            // Clear local cache too
            var apiSubjectsProp = serializedObject.FindProperty("apiSubjects");
            apiSubjectsProp.ClearArray();
            serializedObject.ApplyModifiedProperties();
            
            testFetchResult = "🗑️ Đã xóa cache!";
        }

        private void CheckFetchResult()
        {
            Repaint();
        }

        private void DrawAPIPreview()
        {
            if (QuizAPIService.Instance == null || !QuizAPIService.Instance.IsCacheValid())
            {
                EditorGUILayout.HelpBox("Chưa có dữ liệu. Nhấn 'Fetch API Data' để tải.", MessageType.None);
                return;
            }

            var subjects = QuizAPIService.Instance.GetCachedSubjects();
            if (subjects.Count == 0)
            {
                EditorGUILayout.HelpBox("API không trả về dữ liệu subjects.", MessageType.Warning);
                return;
            }

            EditorGUI.indentLevel++;
            foreach (var subject in subjects)
            {
                EditorGUILayout.LabelField($"📚 {subject.Name} (Lớp {subject.Grade})", EditorStyles.boldLabel);
                
                EditorGUI.indentLevel++;
                foreach (var chapter in subject.Chapters)
                {
                    EditorGUILayout.LabelField($"📖 {chapter.Name} - {chapter.Questions.Count} câu hỏi");
                }
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space(3);
            }
            EditorGUI.indentLevel--;
        }
    }
}
