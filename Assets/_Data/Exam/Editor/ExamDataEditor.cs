using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using HMStudio.EasyQuiz;

namespace Gameplay.Exam.Editor
{
    /// <summary>
    /// Custom Editor cho ExamData - hỗ trợ dropdown chọn GuideData, Steps và Quiz Subject/Chapter
    /// </summary>
    [CustomEditor(typeof(ExamData))]
    public class ExamDataEditor : UnityEditor.Editor
    {
        // Cache for GuideData assets
        private static List<GuideData> cachedGuideData;
        private static string[] cachedGuideNames;
        private static double lastGuidesCacheTime;
        private const double CACHE_DURATION = 5.0;

        // Cache for QuizDatabase
        private static QuizDatabase cachedQuizDatabase;
        private static double lastQuizCacheTime;

        // Foldout states
        private Dictionary<int, bool> sectionFoldouts = new Dictionary<int, bool>();
        private Dictionary<int, bool> previewFoldouts = new Dictionary<int, bool>();

        // ReorderableList for sections
        private ReorderableList sectionsList;
        private SerializedProperty sectionsProperty;

        // ReorderableList for steps (per section index)
        private Dictionary<int, ReorderableList> stepsLists = new Dictionary<int, ReorderableList>();

        private void OnEnable()
        {
            sectionsProperty = serializedObject.FindProperty("sections");
            
            sectionsList = new ReorderableList(serializedObject, sectionsProperty, true, true, true, true);
            
            // Header
            sectionsList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Các phần thi (Kéo để sắp xếp)");
            };

            // Element height
            sectionsList.elementHeightCallback = (int index) =>
            {
                return GetSectionHeight(index);
            };

            // Draw element
            sectionsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = sectionsProperty.GetArrayElementAtIndex(index);
                DrawSectionElement(rect, element, index);
            };

            // On add
            sectionsList.onAddCallback = (ReorderableList list) =>
            {
                int newIndex = list.serializedProperty.arraySize;
                list.serializedProperty.InsertArrayElementAtIndex(newIndex);
                sectionFoldouts[newIndex] = true;
            };

            // On remove
            sectionsList.onRemoveCallback = (ReorderableList list) =>
            {
                if (EditorUtility.DisplayDialog("Xác nhận", "Bạn có chắc muốn xóa section này?", "Xóa", "Hủy"))
                {
                    stepsLists.Remove(list.index);
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                }
            };

            // On reorder
            sectionsList.onReorderCallback = (ReorderableList list) =>
            {
                var newFoldouts = new Dictionary<int, bool>();
                var newPreviews = new Dictionary<int, bool>();
                stepsLists.Clear();
                
                for (int i = 0; i < list.serializedProperty.arraySize; i++)
                {
                    newFoldouts[i] = true;
                    newPreviews[i] = false;
                }
                
                sectionFoldouts = newFoldouts;
                previewFoldouts = newPreviews;
            };
        }

        private float GetSectionHeight(int index)
        {
            if (!sectionFoldouts.ContainsKey(index)) sectionFoldouts[index] = false;
            
            if (!sectionFoldouts[index])
                return EditorGUIUtility.singleLineHeight + 8;

            var element = sectionsProperty.GetArrayElementAtIndex(index);
            var sectionType = (ExamSectionType)element.FindPropertyRelative("sectionType").enumValueIndex;
            
            float lineHeight = 20f;
            float height = lineHeight; // Header foldout
            height += lineHeight * 3; // sectionId, sectionName, sectionType
            height += lineHeight; // "Điểm số" label
            height += lineHeight * 2; // maxScore, weight
            height += 5; // Spacing

            if (sectionType == ExamSectionType.Quiz)
            {
                height += lineHeight; // "Quiz Settings" label
                
                // Check if QuizDatabase exists for warning box
                if (cachedQuizDatabase == null)
                    height += 40; // Warning box
                    
                height += lineHeight * 4; // subject, chapter, questionCount, shuffleQuestions
            }
            else if (sectionType == ExamSectionType.Experiment)
            {
                height += lineHeight; // "Experiment Settings" label
                height += lineHeight; // Guide dropdown
                height += lineHeight; // Preview foldout header
                
                // Preview content
                if (previewFoldouts.ContainsKey(index) && previewFoldouts[index])
                {
                    string guideId = element.FindPropertyRelative("experimentName").stringValue;
                    GuideData guide = GetGuideDataById(guideId);
                    if (guide != null && guide.steps != null)
                        height += lineHeight * guide.steps.Count;
                    else
                        height += 40;
                }

                height += lineHeight; // pointPerStep
                height += 10; // Spacing

                // Steps ReorderableList
                var stepIds = element.FindPropertyRelative("requiredStepIds");
                height += GetStepsListHeight(stepIds);
            }

            return height + 20;
        }

        private float GetStepsListHeight(SerializedProperty stepsProp)
        {
            float headerHeight = 20f;
            float elementHeight = 22f;
            float footerHeight = 22f;
            
            int count = stepsProp.arraySize;
            return headerHeight + (elementHeight * Mathf.Max(count, 1)) + footerHeight;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RefreshCacheIfNeeded();

            // === Header Info ===
            EditorGUILayout.LabelField("Thông tin bài kiểm tra", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("examId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("examName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

            EditorGUILayout.Space(10);

            // === Time Config ===
            EditorGUILayout.LabelField("Cấu hình thời gian", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("examDurationMinutes"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowGoBack"));

            EditorGUILayout.Space(10);

            // === Score Config ===
            EditorGUILayout.LabelField("Cấu hình điểm", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxScore"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("passScore"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("penaltyForWrong"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("penaltyPercent"));

            EditorGUILayout.Space(10);

            // === Sections (ReorderableList) ===
            sectionsList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSectionElement(Rect rect, SerializedProperty sectionProp, int index)
        {
            if (!sectionFoldouts.ContainsKey(index)) sectionFoldouts[index] = false;

            float lineHeight = 20f;
            float fieldHeight = EditorGUIUtility.singleLineHeight;
            Rect currentRect = new Rect(rect.x + 10, rect.y + 2, rect.width - 10, fieldHeight);

            // Header foldout
            string sectionName = sectionProp.FindPropertyRelative("sectionName").stringValue;
            string sectionType = sectionProp.FindPropertyRelative("sectionType").enumDisplayNames[
                sectionProp.FindPropertyRelative("sectionType").enumValueIndex];
            if (string.IsNullOrEmpty(sectionName)) sectionName = $"Section {index}";
            
            sectionFoldouts[index] = EditorGUI.Foldout(currentRect, sectionFoldouts[index], $"{sectionName} [{sectionType}]", true);
            currentRect.y += lineHeight;

            if (!sectionFoldouts[index]) return;

            float labelWidth = EditorGUIUtility.labelWidth;
            Rect labelRect = new Rect(currentRect.x + 15, currentRect.y, labelWidth - 15, fieldHeight);
            Rect fieldRect = new Rect(currentRect.x + labelWidth, currentRect.y, currentRect.width - labelWidth - 15, fieldHeight);

            // === Basic Info ===
            DrawStringField(ref labelRect, ref fieldRect, ref currentRect, lineHeight, "Section Id", sectionProp.FindPropertyRelative("sectionId"));
            DrawStringField(ref labelRect, ref fieldRect, ref currentRect, lineHeight, "Section Name", sectionProp.FindPropertyRelative("sectionName"));
            
            // Section Type
            EditorGUI.LabelField(labelRect, "Section Type");
            sectionProp.FindPropertyRelative("sectionType").enumValueIndex = (int)(ExamSectionType)EditorGUI.EnumPopup(fieldRect, (ExamSectionType)sectionProp.FindPropertyRelative("sectionType").enumValueIndex);
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;

            // === Điểm số ===
            EditorGUI.LabelField(new Rect(currentRect.x + 15, currentRect.y, currentRect.width, fieldHeight), "Điểm số", EditorStyles.boldLabel);
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;

            DrawFloatField(ref labelRect, ref fieldRect, ref currentRect, lineHeight, "Max Score", sectionProp.FindPropertyRelative("maxScore"));
            
            // Weight Slider
            EditorGUI.LabelField(labelRect, "Weight");
            sectionProp.FindPropertyRelative("weight").floatValue = EditorGUI.Slider(fieldRect, sectionProp.FindPropertyRelative("weight").floatValue, 0f, 1f);
            currentRect.y += lineHeight + 5; labelRect.y += lineHeight + 5; fieldRect.y += lineHeight + 5;

            var enumType = (ExamSectionType)sectionProp.FindPropertyRelative("sectionType").enumValueIndex;

            if (enumType == ExamSectionType.Quiz)
            {
                DrawQuizSettings(ref currentRect, ref labelRect, ref fieldRect, lineHeight, fieldHeight, sectionProp);
            }
            else if (enumType == ExamSectionType.Experiment)
            {
                DrawExperimentSettings(ref currentRect, ref labelRect, ref fieldRect, lineHeight, fieldHeight, sectionProp, index);
            }
        }

        private void DrawQuizSettings(ref Rect currentRect, ref Rect labelRect, ref Rect fieldRect, float lineHeight, float fieldHeight, SerializedProperty sectionProp)
        {
            // Quiz Settings Header
            EditorGUI.LabelField(new Rect(currentRect.x + 15, currentRect.y, currentRect.width, fieldHeight), "Quiz Settings", EditorStyles.boldLabel);
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;

            var subjectProp = sectionProp.FindPropertyRelative("subjectIndex");
            var chapterProp = sectionProp.FindPropertyRelative("chapterIndex");

            // Check QuizDatabase
            if (cachedQuizDatabase == null)
            {
                EditorGUI.HelpBox(new Rect(currentRect.x + 15, currentRect.y, currentRect.width - 15, 36), 
                    "QuizDatabase not found! Create via EasyQuiz menu or fallback to manual input.", MessageType.Warning);
                currentRect.y += 40; labelRect.y += 40; fieldRect.y += 40;
                
                // Fallback to manual input
                DrawIntField(ref labelRect, ref fieldRect, ref currentRect, lineHeight, "Subject Index", subjectProp);
                DrawIntField(ref labelRect, ref fieldRect, ref currentRect, lineHeight, "Chapter Index", chapterProp);
            }
            else
            {
                // Subject Dropdown
                string[] subjectNames = GetSubjectNames();
                EditorGUI.LabelField(labelRect, "Subject");
                int newSubjectIndex = EditorGUI.Popup(fieldRect, subjectProp.intValue, subjectNames);
                if (newSubjectIndex != subjectProp.intValue)
                {
                    subjectProp.intValue = newSubjectIndex;
                    chapterProp.intValue = 0; // Reset chapter when subject changes
                }
                currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;

                // Chapter Dropdown
                string[] chapterNames = GetChapterNames(subjectProp.intValue);
                EditorGUI.LabelField(labelRect, "Chapter");
                chapterProp.intValue = EditorGUI.Popup(fieldRect, chapterProp.intValue, chapterNames);
                currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;
            }

            DrawIntField(ref labelRect, ref fieldRect, ref currentRect, lineHeight, "Question Count", sectionProp.FindPropertyRelative("questionCount"));
            
            // Shuffle Questions
            EditorGUI.LabelField(labelRect, "Shuffle Questions");
            sectionProp.FindPropertyRelative("shuffleQuestions").boolValue = EditorGUI.Toggle(fieldRect, sectionProp.FindPropertyRelative("shuffleQuestions").boolValue);
        }

        private void DrawExperimentSettings(ref Rect currentRect, ref Rect labelRect, ref Rect fieldRect, float lineHeight, float fieldHeight, SerializedProperty sectionProp, int sectionIndex)
        {
            // Experiment Settings Header
            EditorGUI.LabelField(new Rect(currentRect.x + 15, currentRect.y, currentRect.width, fieldHeight), "Experiment Settings", EditorStyles.boldLabel);
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;

            // Guide Dropdown
            DrawGuideDropdownRect(new Rect(currentRect.x + 15, currentRect.y, currentRect.width - 15, fieldHeight), sectionProp.FindPropertyRelative("experimentName"));
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;

            // Preview
            DrawGuidePreviewRect(ref currentRect, sectionProp.FindPropertyRelative("experimentName").stringValue, sectionIndex, lineHeight);
            labelRect.y = currentRect.y; fieldRect.y = currentRect.y;

            // Point Per Step
            DrawFloatField(ref labelRect, ref fieldRect, ref currentRect, lineHeight, "Point Per Step", sectionProp.FindPropertyRelative("pointPerStep"));
            currentRect.y += 10; labelRect.y += 10; fieldRect.y += 10;

            // Steps ReorderableList
            DrawStepsReorderableList(ref currentRect, sectionProp.FindPropertyRelative("requiredStepIds"), 
                              sectionProp.FindPropertyRelative("experimentName").stringValue, sectionIndex);
        }

        private void DrawStepsReorderableList(ref Rect currentRect, SerializedProperty stepsProp, string guideId, int sectionIndex)
        {
            // Get or create ReorderableList for this section
            if (!stepsLists.ContainsKey(sectionIndex))
            {
                CreateStepsReorderableList(stepsProp, guideId, sectionIndex);
            }

            // Update the property reference (important!)
            var stepsList = stepsLists[sectionIndex];
            stepsList.serializedProperty = stepsProp;

            // Calculate rect for the list
            float listHeight = GetStepsListHeight(stepsProp);
            Rect listRect = new Rect(currentRect.x + 15, currentRect.y, currentRect.width - 30, listHeight);
            
            // Store guideId for use in callbacks
            stepsList.list = new object[] { guideId };
            
            stepsList.DoList(listRect);
            currentRect.y += listHeight;
        }

        private void CreateStepsReorderableList(SerializedProperty stepsProp, string guideId, int sectionIndex)
        {
            var stepsList = new ReorderableList(stepsProp.serializedObject, stepsProp, true, true, true, true);
            
            stepsList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Required Steps (Kéo để sắp xếp thứ tự)");
            };

            stepsList.elementHeightCallback = (int idx) => 20f;

            stepsList.drawElementCallback = (Rect rect, int idx, bool isActive, bool isFocused) =>
            {
                if (idx >= stepsList.serializedProperty.arraySize) return;
                
                var element = stepsList.serializedProperty.GetArrayElementAtIndex(idx);
                
                // Get guideId from list context
                string gId = "";
                if (stepsList.list is object[] context && context.Length > 0)
                    gId = context[0] as string ?? "";
                
                GuideData guide = GetGuideDataById(gId);
                string[] stepOptions = GetStepOptions(guide);

                int selectedIndex = 0;
                if (guide != null && guide.steps != null)
                {
                    for (int j = 0; j < guide.steps.Count; j++)
                    {
                        if (guide.steps[j].stepID == element.stringValue)
                        {
                            selectedIndex = j + 1;
                            break;
                        }
                    }
                }

                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                int newIndex = EditorGUI.Popup(rect, $"Step {idx + 1}", selectedIndex, stepOptions);
                if (newIndex != selectedIndex && newIndex > 0 && guide != null && guide.steps != null)
                {
                    element.stringValue = guide.steps[newIndex - 1].stepID;
                }
            };

            stepsList.onAddCallback = (ReorderableList list) =>
            {
                string gId = "";
                if (list.list is object[] context && context.Length > 0)
                    gId = context[0] as string ?? "";
                    
                GuideData guide = GetGuideDataById(gId);
                int newIndex = list.serializedProperty.arraySize;
                list.serializedProperty.InsertArrayElementAtIndex(newIndex);
                
                // Set default value to first available step
                if (guide != null && guide.steps != null && guide.steps.Count > 0)
                {
                    list.serializedProperty.GetArrayElementAtIndex(newIndex).stringValue = guide.steps[0].stepID;
                }
                else
                {
                    list.serializedProperty.GetArrayElementAtIndex(newIndex).stringValue = "";
                }
            };

            stepsLists[sectionIndex] = stepsList;
        }

        // === Helper Draw Methods ===
        private void DrawStringField(ref Rect labelRect, ref Rect fieldRect, ref Rect currentRect, float lineHeight, string label, SerializedProperty prop)
        {
            EditorGUI.LabelField(labelRect, label);
            prop.stringValue = EditorGUI.TextField(fieldRect, prop.stringValue);
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;
        }

        private void DrawIntField(ref Rect labelRect, ref Rect fieldRect, ref Rect currentRect, float lineHeight, string label, SerializedProperty prop)
        {
            EditorGUI.LabelField(labelRect, label);
            prop.intValue = EditorGUI.IntField(fieldRect, prop.intValue);
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;
        }

        private void DrawFloatField(ref Rect labelRect, ref Rect fieldRect, ref Rect currentRect, float lineHeight, string label, SerializedProperty prop)
        {
            EditorGUI.LabelField(labelRect, label);
            prop.floatValue = EditorGUI.FloatField(fieldRect, prop.floatValue);
            currentRect.y += lineHeight; labelRect.y += lineHeight; fieldRect.y += lineHeight;
        }

        // === Quiz Database Helpers ===
        private string[] GetSubjectNames()
        {
            if (cachedQuizDatabase == null) return new string[] { "-- No Database --" };

            List<string> names = new List<string>();
            
            if (cachedQuizDatabase.DataMode == QuizDataMode.Excel)
            {
                foreach (var subject in cachedQuizDatabase.ExcelSubjects)
                {
                    names.Add(subject.Name);
                }
            }
            else // API Mode
            {
                foreach (var subject in cachedQuizDatabase.APISubjects)
                {
                    names.Add($"{subject.Name} (Lớp {subject.Grade})");
                }
            }

            if (names.Count == 0) return new string[] { "-- No Subjects --" };
            return names.ToArray();
        }

        private string[] GetChapterNames(int subjectIndex)
        {
            if (cachedQuizDatabase == null) return new string[] { "-- No Database --" };

            List<string> names = new List<string>();
            
            if (cachedQuizDatabase.DataMode == QuizDataMode.Excel)
            {
                if (subjectIndex >= 0 && subjectIndex < cachedQuizDatabase.ExcelSubjects.Count)
                {
                    foreach (var chapter in cachedQuizDatabase.ExcelSubjects[subjectIndex].Chapters)
                    {
                        names.Add(chapter.Name);
                    }
                }
            }
            else // API Mode
            {
                if (subjectIndex >= 0 && subjectIndex < cachedQuizDatabase.APISubjects.Count)
                {
                    foreach (var chapter in cachedQuizDatabase.APISubjects[subjectIndex].Chapters)
                    {
                        names.Add($"{chapter.Name} ({chapter.QuestionCount} câu)");
                    }
                }
            }

            if (names.Count == 0) return new string[] { "-- No Chapters --" };
            return names.ToArray();
        }

        // === Guide Helpers ===
        private void DrawGuideDropdownRect(Rect rect, SerializedProperty prop)
        {
            int currentIndex = 0;
            string currentId = prop.stringValue;

            if (cachedGuideData != null)
            {
                for (int i = 0; i < cachedGuideData.Count; i++)
                {
                    if (cachedGuideData[i].guideID == currentId)
                    {
                        currentIndex = i + 1;
                        break;
                    }
                }
            }

            int optionsCount = (cachedGuideNames?.Length ?? 0) + 1;
            string[] options = new string[optionsCount];
            options[0] = "-- Select Guide --";
            if (cachedGuideNames != null && cachedGuideNames.Length > 0)
                System.Array.Copy(cachedGuideNames, 0, options, 1, cachedGuideNames.Length);

            int newIndex = EditorGUI.Popup(rect, "Experiment Guide", currentIndex, options);
            if (newIndex != currentIndex)
            {
                prop.stringValue = (newIndex == 0 || cachedGuideData == null || newIndex - 1 >= cachedGuideData.Count) 
                    ? "" : cachedGuideData[newIndex - 1].guideID;
            }
        }

        private void DrawGuidePreviewRect(ref Rect currentRect, string guideId, int sectionIndex, float lineHeight)
        {
            if (!previewFoldouts.ContainsKey(sectionIndex)) previewFoldouts[sectionIndex] = false;

            previewFoldouts[sectionIndex] = EditorGUI.Foldout(currentRect, previewFoldouts[sectionIndex], "Preview Guide Steps");
            currentRect.y += lineHeight;

            if (previewFoldouts[sectionIndex])
            {
                GuideData guide = GetGuideDataById(guideId);
                if (guide != null && guide.steps != null)
                {
                    EditorGUI.indentLevel++;
                    GUI.enabled = false;
                    foreach (var step in guide.steps)
                    {
                        EditorGUI.LabelField(currentRect, $"• {step.stepID}: {step.title}");
                        currentRect.y += lineHeight;
                    }
                    GUI.enabled = true;
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUI.HelpBox(new Rect(currentRect.x + 15, currentRect.y, currentRect.width - 15, 36), 
                        "Guide not found or not selected", MessageType.Warning);
                    currentRect.y += 40;
                }
            }
        }

        private GuideData GetGuideDataById(string id)
        {
            if (string.IsNullOrEmpty(id) || cachedGuideData == null) return null;
            return cachedGuideData.FirstOrDefault(g => g.guideID == id);
        }

        private string[] GetStepOptions(GuideData guide)
        {
            if (guide == null || guide.steps == null) return new string[] { "-- No Guide Selected --" };

            string[] options = new string[guide.steps.Count + 1];
            options[0] = "-- Select Step --";
            for (int i = 0; i < guide.steps.Count; i++)
            {
                options[i + 1] = $"{guide.steps[i].stepID} - {guide.steps[i].title}";
            }
            return options;
        }

        // === Cache Management ===
        private void RefreshCacheIfNeeded()
        {
            double currentTime = EditorApplication.timeSinceStartup;

            // Refresh GuideData cache
            if (currentTime - lastGuidesCacheTime > CACHE_DURATION || cachedGuideData == null)
            {
                cachedGuideData = new List<GuideData>();
                string[] guids = AssetDatabase.FindAssets("t:GuideData");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GuideData g = AssetDatabase.LoadAssetAtPath<GuideData>(path);
                    if (g != null) cachedGuideData.Add(g);
                }
                cachedGuideNames = cachedGuideData.Select(g => $"{g.guideID} ({g.name})").ToArray();
                lastGuidesCacheTime = currentTime;
            }

            // Refresh QuizDatabase cache
            if (currentTime - lastQuizCacheTime > CACHE_DURATION || cachedQuizDatabase == null)
            {
                string[] quizGuids = AssetDatabase.FindAssets("t:QuizDatabase");
                if (quizGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(quizGuids[0]);
                    cachedQuizDatabase = AssetDatabase.LoadAssetAtPath<QuizDatabase>(path);
                }
                lastQuizCacheTime = currentTime;
            }
        }
    }
}
