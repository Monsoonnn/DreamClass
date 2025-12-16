using UnityEngine;
using UnityEditor;
using DreamClass.Subjects;
using System.Linq;
using System.Collections.Generic;

namespace DreamClass.QuestSystem
{
    [CustomEditor(typeof(ReadingQuestStep))]
    public class ReadingQuestStepEditor : Editor
    {
        private SubjectDatabase subjectDatabase;
        private int selectedSubjectIndex = -1;
        private int selectedLectureIndex = -1;
        private int selectedEndLectureIndex = -1;
        private int selectedChapterIndex = -1;

        private string[] subjectNames;
        private string[] lectureNames;
        private string[] chapterNames;

        private enum SelectionMode
        {
            SingleLecture,
            LectureRange,
            WholeChapter,
            RandomLecture,
            RandomChapter,
            RandomSubject  // <-- MỚI
        }
        private SelectionMode currentMode = SelectionMode.SingleLecture;

        private void OnEnable()
        {
            LoadDatabase();
            InitializeSelectionFromTarget();
            LoadModeFromTarget();
        }
        
        private void LoadModeFromTarget()
        {
            ReadingQuestStep step = (ReadingQuestStep)target;
            
            if (step.isRandom)
            {
                switch (step.randomMode)
                {
                    case ReadingQuestStep.RandomMode.RandomLecture:
                        currentMode = SelectionMode.RandomLecture;
                        break;
                    case ReadingQuestStep.RandomMode.RandomChapter:
                        currentMode = SelectionMode.RandomChapter;
                        break;
                    case ReadingQuestStep.RandomMode.RandomSubject:
                        currentMode = SelectionMode.RandomSubject;
                        break;
                }
            }
            else if (step.lectureName.Contains(" - "))
            {
                currentMode = SelectionMode.LectureRange;
            }
            else if (step.lectureName.Contains("All lectures"))
            {
                currentMode = SelectionMode.WholeChapter;
            }
            else
            {
                currentMode = SelectionMode.SingleLecture;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            if (subjectDatabase == null)
            {
                EditorGUILayout.HelpBox("SubjectDatabase not found. Please create one.", MessageType.Error);
                if (GUILayout.Button("Retry Load"))
                {
                    LoadDatabase();
                }
                return;
            }

            ReadingQuestStep step = (ReadingQuestStep)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Lecture Selector", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Validation: Check if database reference is assigned
            if (step.database == null)
            {
                EditorGUILayout.HelpBox("SubjectDatabase reference is missing! It will be auto-assigned when you apply a selection.", MessageType.Warning);
            }

            // Display current configuration
            if (!string.IsNullOrEmpty(step.lectureName))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Current Configuration", EditorStyles.boldLabel);
                
                string modeText = GetModeDisplayText(step);
                
                EditorGUILayout.LabelField("Mode:", modeText);
                
                if (step.randomMode != ReadingQuestStep.RandomMode.RandomSubject)
                {
                    EditorGUILayout.LabelField("Subject:", step.subjectName);
                }
                else
                {
                    EditorGUILayout.LabelField("Subject:", "[Will be randomly selected at runtime]");
                }
                
                EditorGUILayout.LabelField("Lecture:", step.lectureName);
                
                if (!step.isRandom)
                {
                    EditorGUILayout.LabelField("Pages:", $"{step.startPage} - {step.endPage}");
                }
                else
                {
                    EditorGUILayout.LabelField("Pages:", "Will be determined at runtime");
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Selection Mode (luôn hiển thị đầu tiên)
            EditorGUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            currentMode = (SelectionMode)EditorGUILayout.EnumPopup("Selection Mode", currentMode);
            if (EditorGUI.EndChangeCheck())
            {
                // Reset selections when mode changes
                selectedSubjectIndex = 0;
                selectedLectureIndex = 0;
                selectedEndLectureIndex = 0;
                selectedChapterIndex = 0;
            }
            
            EditorGUILayout.Space(5);

            // CHỈ HIỂN THỊ SUBJECT SELECTOR NẾU KHÔNG PHẢI RANDOM SUBJECT
            if (currentMode != SelectionMode.RandomSubject)
            {
                EditorGUI.BeginChangeCheck();
                selectedSubjectIndex = EditorGUILayout.Popup("Subject", selectedSubjectIndex, subjectNames);
                if (EditorGUI.EndChangeCheck())
                {
                    selectedLectureIndex = 0;
                    selectedEndLectureIndex = 0;
                    selectedChapterIndex = 0;
                }

                if (selectedSubjectIndex > 0)
                {
                    SubjectInfo selectedSubject = subjectDatabase.subjects[selectedSubjectIndex - 1];
                    UpdateLectureNames(selectedSubject);
                    UpdateChapterNames(selectedSubject);

                    EditorGUILayout.Space(5);
                    DrawModeUI(step, selectedSubject);
                }
            }
            else
            {
                // RANDOM SUBJECT MODE - không cần chọn subject
                DrawRandomSubjectMode(step);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private string GetModeDisplayText(ReadingQuestStep step)
        {
            if (step.isRandom)
            {
                switch (step.randomMode)
                {
                    case ReadingQuestStep.RandomMode.RandomLecture:
                        return "Random Lecture";
                    case ReadingQuestStep.RandomMode.RandomChapter:
                        return "Random Chapter";
                    case ReadingQuestStep.RandomMode.RandomSubject:
                        return "Random Subject";
                }
            }
            
            if (step.lectureName.Contains(" - "))
                return "Lecture Range";
            if (step.lectureName.Contains("All lectures"))
                return "Whole Chapter";
            
            return "Single Lecture";
        }

        private void DrawModeUI(ReadingQuestStep step, SubjectInfo subject)
        {
            switch (currentMode)
            {
                case SelectionMode.SingleLecture:
                    DrawSingleLectureMode(step, subject);
                    break;
                case SelectionMode.LectureRange:
                    DrawLectureRangeMode(step, subject);
                    break;
                case SelectionMode.WholeChapter:
                    DrawWholeChapterMode(step, subject);
                    break;
                case SelectionMode.RandomLecture:
                    DrawRandomLectureMode(step, subject);
                    break;
                case SelectionMode.RandomChapter:
                    DrawRandomChapterMode(step, subject);
                    break;
            }
        }

        private void DrawSingleLectureMode(ReadingQuestStep step, SubjectInfo subject)
        {
            EditorGUILayout.HelpBox("Select a single lecture", MessageType.Info);
            selectedLectureIndex = EditorGUILayout.Popup("Lecture", selectedLectureIndex, lectureNames);

            if (selectedLectureIndex > 0)
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Apply Single Lecture"))
                {
                    ApplySingleLecture(step, subject, selectedLectureIndex - 1);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawLectureRangeMode(ReadingQuestStep step, SubjectInfo subject)
        {
            EditorGUILayout.HelpBox("Select multiple consecutive lectures", MessageType.Info);
            selectedLectureIndex = EditorGUILayout.Popup("Start Lecture", selectedLectureIndex, lectureNames);
            selectedEndLectureIndex = EditorGUILayout.Popup("End Lecture", selectedEndLectureIndex, lectureNames);

            if (selectedLectureIndex > 0 && selectedEndLectureIndex > 0)
            {
                if (selectedEndLectureIndex < selectedLectureIndex)
                {
                    EditorGUILayout.HelpBox("End lecture must be after start lecture!", MessageType.Warning);
                }
                else
                {
                    int count = selectedEndLectureIndex - selectedLectureIndex + 1;
                    EditorGUILayout.HelpBox($"Will include {count} lecture(s)", MessageType.None);
                    
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Apply Lecture Range"))
                    {
                        ApplyLectureRange(step, subject, selectedLectureIndex - 1, selectedEndLectureIndex - 1);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
        }

        private void DrawWholeChapterMode(ReadingQuestStep step, SubjectInfo subject)
        {
            EditorGUILayout.HelpBox("Select an entire chapter (all lectures in it)", MessageType.Info);
            selectedChapterIndex = EditorGUILayout.Popup("Chapter", selectedChapterIndex, chapterNames);

            if (selectedChapterIndex > 0)
            {
                int chapterNum = GetChapterNumber(subject, selectedChapterIndex - 1);
                var lecturesInChapter = subject.lectures.Where(l => l.chapter == chapterNum).ToList();
                
                EditorGUILayout.HelpBox($"Chapter {chapterNum} contains {lecturesInChapter.Count} lecture(s)", MessageType.None);
                
                GUI.backgroundColor = Color.blue;
                if (GUILayout.Button("Apply Whole Chapter"))
                {
                    ApplyWholeChapter(step, subject, chapterNum);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawRandomLectureMode(ReadingQuestStep step, SubjectInfo subject)
        {
            EditorGUILayout.HelpBox($"Will randomly select ONE lecture from '{subject.name}' when player receives the quest.", MessageType.Info);
            
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Set Random Lecture Mode"))
            {
                ApplyRandomLecture(step, subject);
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawRandomChapterMode(ReadingQuestStep step, SubjectInfo subject)
        {
            EditorGUILayout.HelpBox($"Will randomly select ONE CHAPTER from '{subject.name}' when player receives the quest.", MessageType.Info);
            
            GUI.backgroundColor = Color.magenta;
            if (GUILayout.Button("Set Random Chapter Mode"))
            {
                ApplyRandomChapter(step, subject);
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawRandomSubjectMode(ReadingQuestStep step)
        {
            EditorGUILayout.HelpBox("Will randomly select ONE SUBJECT and ONE LECTURE from it when player receives the quest.", MessageType.Info);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Available Subjects: {subjectDatabase.subjects.Count}", EditorStyles.boldLabel);
            foreach (var subject in subjectDatabase.subjects)
            {
                EditorGUILayout.LabelField($"• {subject.GetDisplayName()} ({subject.lectures.Count} lectures)");
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            GUI.backgroundColor = new Color(1f, 0.5f, 0f); // Orange
            if (GUILayout.Button("Set Random Subject Mode", GUILayout.Height(30)))
            {
                ApplyRandomSubject(step);
            }
            GUI.backgroundColor = Color.white;
        }

        // ============= APPLY METHODS =============

        private void ApplySingleLecture(ReadingQuestStep step, SubjectInfo subject, int lectureIndex)
        {
            if (lectureIndex < 0 || lectureIndex >= subject.lectures.Count) return;

            CSVLectureInfo lecture = subject.lectures[lectureIndex];
            Undo.RecordObject(step, "Apply Single Lecture");

            step.lectureName = lecture.lectureName;
            step.chapterName = $"Chapter {lecture.chapter}: {lecture.groupName}";
            step.startPage = lecture.page;
            step.endPage = CalculateEndPage(subject, lectureIndex);
            step.isRandom = false;
            step.randomMode = ReadingQuestStep.RandomMode.None;
            step.subjectName = subject.name;
            step.database = subjectDatabase;

            EditorUtility.SetDirty(step);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Applied single lecture: {lecture.lectureName} (Pages {step.startPage}-{step.endPage})");
        }

        private void ApplyLectureRange(ReadingQuestStep step, SubjectInfo subject, int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex >= subject.lectures.Count) return;

            CSVLectureInfo startLecture = subject.lectures[startIndex];
            CSVLectureInfo endLecture = subject.lectures[endIndex];
            
            Undo.RecordObject(step, "Apply Lecture Range");

            step.lectureName = $"{startLecture.lectureName} - {endLecture.lectureName}";
            step.chapterName = $"Chapter {startLecture.chapter}: {startLecture.groupName}";
            step.startPage = startLecture.page;
            step.endPage = CalculateEndPage(subject, endIndex);
            step.isRandom = false;
            step.randomMode = ReadingQuestStep.RandomMode.None;
            step.subjectName = subject.name;
            step.database = subjectDatabase;

            EditorUtility.SetDirty(step);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Applied lecture range: {step.lectureName} (Pages {step.startPage}-{step.endPage})");
        }

        private void ApplyWholeChapter(ReadingQuestStep step, SubjectInfo subject, int chapterNum)
        {
            var lecturesInChapter = subject.lectures.Where(l => l.chapter == chapterNum).OrderBy(l => l.page).ToList();
            
            if (lecturesInChapter.Count == 0) return;

            Undo.RecordObject(step, "Apply Whole Chapter");

            var firstLecture = lecturesInChapter.First();
            var lastLecture = lecturesInChapter.Last();
            
            step.lectureName = $"All lectures in Chapter {chapterNum}";
            step.chapterName = $"Chapter {chapterNum}: {firstLecture.groupName}";
            step.startPage = firstLecture.page;
            
            int lastLectureIndex = subject.lectures.IndexOf(lastLecture);
            step.endPage = CalculateEndPage(subject, lastLectureIndex);
            
            step.isRandom = false;
            step.randomMode = ReadingQuestStep.RandomMode.None;
            step.subjectName = subject.name;
            step.database = subjectDatabase;

            EditorUtility.SetDirty(step);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Applied whole chapter {chapterNum}: {lecturesInChapter.Count} lectures (Pages {step.startPage}-{step.endPage})");
        }

        private void ApplyRandomLecture(ReadingQuestStep step, SubjectInfo subject)
        {
            Undo.RecordObject(step, "Set Random Lecture Mode");

            step.lectureName = "[Random Lecture]";
            step.chapterName = "[Will be determined at runtime]";
            step.startPage = 0;
            step.endPage = 0;
            step.isRandom = true;
            step.randomMode = ReadingQuestStep.RandomMode.RandomLecture;
            step.subjectName = subject.name;
            step.database = subjectDatabase;

            EditorUtility.SetDirty(step);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Set to random lecture mode for subject: {subject.name}");
        }

        private void ApplyRandomChapter(ReadingQuestStep step, SubjectInfo subject)
        {
            Undo.RecordObject(step, "Set Random Chapter Mode");

            step.lectureName = "[Random Chapter]";
            step.chapterName = "[Will be determined at runtime]";
            step.startPage = 0;
            step.endPage = 0;
            step.isRandom = true;
            step.randomMode = ReadingQuestStep.RandomMode.RandomChapter;
            step.subjectName = subject.name;
            step.database = subjectDatabase;

            EditorUtility.SetDirty(step);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Set to random chapter mode for subject: {subject.name}");
        }

        private void ApplyRandomSubject(ReadingQuestStep step)
        {
            Undo.RecordObject(step, "Set Random Subject Mode");

            step.lectureName = "[Random Subject & Lecture]";
            step.chapterName = "[Will be determined at runtime]";
            step.startPage = 0;
            step.endPage = 0;
            step.isRandom = true;
            step.randomMode = ReadingQuestStep.RandomMode.RandomSubject;
            step.subjectName = ""; // Không set subject cụ thể
            step.database = subjectDatabase;

            EditorUtility.SetDirty(step);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Set to random subject mode (will pick from {subjectDatabase.subjects.Count} subjects)");
        }

        // ============= HELPER METHODS =============

        private int CalculateEndPage(SubjectInfo subject, int lectureIndex)
        {
            int nextLectureIndex = lectureIndex + 1;
            if (nextLectureIndex < subject.lectures.Count)
            {
                return subject.lectures[nextLectureIndex].page - 1;
            }
            else
            {
                return subject.pages > 0 ? subject.pages : subject.lectures[lectureIndex].page + 10;
            }
        }

        private int GetChapterNumber(SubjectInfo subject, int chapterIndex)
        {
            var chapters = subject.lectures.Select(l => l.chapter).Distinct().OrderBy(c => c).ToList();
            return chapterIndex < chapters.Count ? chapters[chapterIndex] : 0;
        }

        private void LoadDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:SubjectDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                subjectDatabase = AssetDatabase.LoadAssetAtPath<SubjectDatabase>(path);
                UpdateSubjectNames();
            }
        }

        private void InitializeSelectionFromTarget()
        {
            ReadingQuestStep step = (ReadingQuestStep)target;
            if (subjectDatabase == null || string.IsNullOrEmpty(step.lectureName))
            {
                selectedSubjectIndex = 0;
                selectedLectureIndex = 0;
                return;
            }

            // Không load selection nếu là random subject
            if (step.randomMode == ReadingQuestStep.RandomMode.RandomSubject)
            {
                return;
            }

            for (int i = 0; i < subjectDatabase.subjects.Count; i++)
            {
                var subject = subjectDatabase.subjects[i];
                if (subject.name == step.subjectName)
                {
                    selectedSubjectIndex = i + 1;
                    UpdateLectureNames(subject);
                    
                    for (int j = 0; j < subject.lectures.Count; j++)
                    {
                        if (subject.lectures[j].lectureName == step.lectureName &&
                            subject.lectures[j].page == step.startPage)
                        {
                            selectedLectureIndex = j + 1;
                            break;
                        }
                    }
                    break;
                }
            }
        }

        private void UpdateSubjectNames()
        {
            if (subjectDatabase == null) return;
            subjectNames = new string[subjectDatabase.subjects.Count + 1];
            subjectNames[0] = "None";
            for (int i = 0; i < subjectDatabase.subjects.Count; i++)
            {
                subjectNames[i + 1] = subjectDatabase.subjects[i].GetDisplayName();
            }
        }

        private void UpdateLectureNames(SubjectInfo subject)
        {
            lectureNames = new string[subject.lectures.Count + 1];
            lectureNames[0] = "None";
            for (int i = 0; i < subject.lectures.Count; i++)
            {
                lectureNames[i + 1] = $"{subject.lectures[i].chapter}-{subject.lectures[i].groupName}: {subject.lectures[i].lectureName}";
            }
        }

        private void UpdateChapterNames(SubjectInfo subject)
        {
            var chapters = subject.lectures.Select(l => l.chapter).Distinct().OrderBy(c => c).ToList();
            chapterNames = new string[chapters.Count + 1];
            chapterNames[0] = "None";
            for (int i = 0; i < chapters.Count; i++)
            {
                var firstLectureInChapter = subject.lectures.First(l => l.chapter == chapters[i]);
                chapterNames[i + 1] = $"Chapter {chapters[i]}: {firstLectureInChapter.groupName}";
            }
        }
    }
}