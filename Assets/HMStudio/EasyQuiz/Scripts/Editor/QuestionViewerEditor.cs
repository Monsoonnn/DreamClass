#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace HMStudio.EasyQuiz
{
    [CustomEditor(typeof(QuestionViewer))]
    public class QuestionViewerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            QuestionViewer qv = (QuestionViewer)target;

            // Thêm hiển thị fields mới ở đầu (quizDatabase, subjectID, chapterID)
            SerializedProperty quizDatabaseProp = serializedObject.FindProperty("quizDatabase");
            EditorGUILayout.PropertyField(quizDatabaseProp);

            SerializedProperty subjectIDProp = serializedObject.FindProperty("subjectID");
            EditorGUILayout.PropertyField(subjectIDProp);

            SerializedProperty chapterIDProp = serializedObject.FindProperty("chapterID");
            EditorGUILayout.PropertyField(chapterIDProp);

            // Giữ nguyên code gốc
            EditorGUILayout.LabelField("Question Viewer", EditorStyles.boldLabel);

            if (qv.questionID < 1)
            {
                qv.questionID = 1;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Prev", GUILayout.Width(60)))
            {
                int total = qv.GetTotalQuestions();
                if (total > 0)
                {
                    qv.questionID--;
                    if (qv.questionID < 1)
                        qv.questionID = total;
                }
                qv.LoadQuestionFromExcel();
            }
            qv.questionID = EditorGUILayout.IntField(qv.questionID, GUILayout.Width(50));
            if (GUILayout.Button("Next", GUILayout.Width(60)))
            {
                int total = qv.GetTotalQuestions();
                if (total > 0)
                {
                    qv.questionID++;
                    if (qv.questionID > total)
                        qv.questionID = 1;
                }
                qv.LoadQuestionFromExcel();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Load Question"))
            {
                qv.LoadQuestionFromExcel();
            }

            int totalQuestions = qv.GetTotalQuestions();
            EditorGUILayout.HelpBox($"Question {qv.questionID} / {totalQuestions}", MessageType.Info);

            if (string.IsNullOrEmpty(qv.questionText))
            {
                EditorGUILayout.HelpBox("Question ID not found!", MessageType.Error);
            }

            EditorGUILayout.LabelField("Question", EditorStyles.boldLabel);
            qv.questionText = EditorGUILayout.TextField(qv.questionText);

            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            for (int i = 0; i < qv.options.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                qv.options[i] = EditorGUILayout.TextField("Option " + (i + 1), qv.options[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    qv.options.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add Option"))
            {
                qv.options.Add("");
            }

            EditorGUILayout.LabelField("Correct Answer", EditorStyles.boldLabel);
            qv.correctAnswer = EditorGUILayout.TextField(qv.correctAnswer);

            if (GUILayout.Button("Update"))
            {
                qv.UpdateExcel();
            }

            if (!string.IsNullOrEmpty(qv.updateStatusMessage))
            {
                MessageType msgType = qv.updateStatusSuccess ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox(qv.updateStatusMessage, msgType);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI Text References (assigned in Inspector)", EditorStyles.boldLabel);
            SerializedProperty tmpQuestionProp = serializedObject.FindProperty("_tmpQuestion");
            EditorGUILayout.PropertyField(tmpQuestionProp);
            SerializedProperty lstOptionsProp = serializedObject.FindProperty("_lstOptions");
            EditorGUILayout.PropertyField(lstOptionsProp, true);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(qv);
            }
        }
    }
}
#endif