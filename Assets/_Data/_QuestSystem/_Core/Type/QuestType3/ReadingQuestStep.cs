using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using DreamClass.Subjects;

namespace DreamClass.QuestSystem
{
    public class ReadingQuestStep : QuestStep
    {
        [Header("Reading Step Settings")]
        public string subjectName;
        public string lectureName;
        public string chapterName;
        public int startPage;
        public int endPage;

        [Header("Random Settings")]
        public bool isRandom;
        public RandomMode randomMode;

        [Header("Completion Settings")]
        [Range(0f, 1f)]
        [Tooltip("Phần trăm trang cần đọc để hoàn thành (0.8 = 80%)")]
        public float completionThreshold = 0.8f;

        [Header("Runtime Reference")]
        [Tooltip("Reference to SubjectDatabase for runtime random selection")]
        public SubjectDatabase database;

        public enum RandomMode
        {
            None,
            RandomLecture,
            RandomChapter,
            RandomSubject
        }

        private BookVR book;
        private bool hasInitializedRandom = false;
        
        // Tracking pages đã đọc
        private HashSet<int> readPages = new HashSet<int>();
        private int totalPages;
        private int requiredPages;

        public override void StartStep()
        {
            base.StartStep();

            // Handle random selection at runtime
            if (isRandom && !hasInitializedRandom)
            {
                Debug.Log($"[ReadingQuestStep] Random selection enabled for step: {StepId}");
                InitializeRandomSelection();
                hasInitializedRandom = true;
            }

            // Initialize tracking
            readPages.Clear();
            totalPages = endPage - startPage + 1;
            requiredPages = Mathf.CeilToInt(totalPages * completionThreshold);

            Debug.Log($"[ReadingQuestStep] Started tracking: Need to read {requiredPages}/{totalPages} pages ({completionThreshold * 100}%)");

            book = Object.FindObjectsByType<BookVR>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            ).FirstOrDefault();
        }

        private void InitializeRandomSelection()
        {
            List<SubjectInfo> subjectsToUse = null;

            // Try getting runtime subjects first
            var pdfService = PDFSubjectService.Instance;
            if (pdfService != null && pdfService.RuntimeSubjects != null && pdfService.RuntimeSubjects.Count > 0)
            {
                subjectsToUse = pdfService.RuntimeSubjects;
            }
            // Fallback to database
            else if (database != null)
            {
                subjectsToUse = database.subjects;
            }
            
            if (subjectsToUse == null || subjectsToUse.Count == 0)
            {
                Debug.LogError("[ReadingQuestStep] No subjects found (checked RuntimeSubjects and Database)!");
                return;
            }

            switch (randomMode)
            {
                case RandomMode.RandomSubject:
                    SelectRandomSubject(subjectsToUse);
                    break;
                case RandomMode.RandomLecture:
                    var subject = subjectsToUse.FirstOrDefault(s => s.name == subjectName);
                    if (subject != null)
                        SelectRandomLecture(subject);
                    break;
                case RandomMode.RandomChapter:
                    var subjectForChapter = subjectsToUse.FirstOrDefault(s => s.name == subjectName);
                    if (subjectForChapter != null)
                        SelectRandomChapter(subjectForChapter);
                    break;
            }
        }

        private void SelectRandomSubject(List<SubjectInfo> subjects)
        {
            if (subjects == null || subjects.Count == 0) return;

            var randomSubject = subjects[Random.Range(0, subjects.Count)];
            subjectName = randomSubject.name;

            SelectRandomLecture(randomSubject);
        }

        private void SelectRandomLecture(SubjectInfo subject)
        {
            if (subject.lectures.Count == 0)
            {
                Debug.LogError("[ReadingQuestStep] No lectures found in subject!");
                return;
            }

            int randomIndex = Random.Range(0, subject.lectures.Count);
            var selectedLecture = subject.lectures[randomIndex];

            lectureName = selectedLecture.lectureName;
            chapterName = $"Chapter {selectedLecture.chapter}: {selectedLecture.groupName}";
            startPage = selectedLecture.page;

            int nextIndex = randomIndex + 1;
            if (nextIndex < subject.lectures.Count)
            {
                endPage = subject.lectures[nextIndex].page - 1;
            }
            else
            {
                endPage = subject.pages > 0 ? subject.pages : book.spriteManager.TotalPages;
            }

            Debug.Log($"[ReadingQuestStep] Random lecture selected: {lectureName} (Pages {startPage}-{endPage})");
        }

        private void SelectRandomChapter(SubjectInfo subject)
        {
            var chapters = subject.lectures.Select(l => l.chapter).Distinct().ToList();
            if (chapters.Count == 0)
            {
                Debug.LogError("[ReadingQuestStep] No chapters found in subject!");
                return;
            }

            int randomChapter = chapters[Random.Range(0, chapters.Count)];
            var lecturesInChapter = subject.lectures.Where(l => l.chapter == randomChapter).OrderBy(l => l.page).ToList();

            var firstLecture = lecturesInChapter.First();
            var lastLecture = lecturesInChapter.Last();

            lectureName = $"All lectures in Chapter {randomChapter}";
            chapterName = $"Chapter {randomChapter}: {firstLecture.groupName}";
            startPage = firstLecture.page;

            int lastLectureIndex = subject.lectures.IndexOf(lastLecture);
            int nextIndex = lastLectureIndex + 1;
            if (nextIndex < subject.lectures.Count)
            {
                endPage = subject.lectures[nextIndex].page - 1;
            }
            else
            {
                endPage = subject.pages > 0 ? subject.pages : book.spriteManager.TotalPages;
            }

            Debug.Log($"[ReadingQuestStep] Random chapter {randomChapter} selected: {lecturesInChapter.Count} lectures (Pages {startPage}-{endPage})");
        }

        public override void OnUpdate(object context)
        {
            if (IsComplete || book == null) return;

            if (context is BookVR bookInstance)
            {
                int currentPage = bookInstance.CurrentPage;
                
                // BookVR trả về page theo spread (2 pages mỗi lần)
                // Ví dụ: 134, 136, 138 thay vì 134, 135, 136, 137, 138
                // Nên cần track cả 2 pages trong spread
                
                if (currentPage >= startPage && currentPage <= endPage)
                {
                    bool hasNewPage = false;
                    
                    // Track page hiện tại (trang trái)
                    if (readPages.Add(currentPage))
                    {
                        hasNewPage = true;
                    }
                    
                    // Track page kế tiếp (trang phải) nếu còn trong range
                    int nextPage = currentPage + 1;
                    if (nextPage <= endPage && readPages.Add(nextPage))
                    {
                        hasNewPage = true;
                    }
                    
                    if (hasNewPage)
                    {
                        Debug.Log($"[ReadingQuestStep] Spread {currentPage}-{nextPage} read. Progress: {readPages.Count}/{requiredPages} ({GetProgress() * 100:F0}%)");
                        
                        // Check completion
                        if (readPages.Count >= requiredPages)
                        {
                            OnComplete();
                        }
                    }
                }
            }
        }

        public override void OnComplete()
        {
            if (IsComplete) return;
            Debug.Log($"[ReadingQuestStep] Completed: {lectureName} - Read {readPages.Count}/{totalPages} pages");
            base.OnComplete();
        }

        // Reset random flag when quest is reassigned
        public void ResetRandomSelection()
        {
            hasInitializedRandom = false;
            readPages.Clear();
        }

        // Public method để get progress (cho UI)
        public float GetProgress()
        {
            if (totalPages == 0) return 0f;
            return (float)readPages.Count / requiredPages;
        }

        public string GetProgressText()
        {
            return $"{readPages.Count}/{requiredPages} pages read ({GetProgress() * 100:F0}%)";
        }

        // Get số spread (2-page views) đã đọc
        public int GetSpreadsRead()
        {
            if (readPages.Count == 0) return 0;
            
            int spreads = 0;
            int currentSpread = -1;
            
            foreach (int page in readPages.OrderBy(p => p))
            {
                int spreadStart = (page % 2 == 0) ? page : page - 1;
                if (spreadStart != currentSpread)
                {
                    spreads++;
                    currentSpread = spreadStart;
                }
            }
            
            return spreads;
        }

        public int GetTotalSpreads()
        {
            return Mathf.CeilToInt(totalPages / 2f);
        }

        public string GetSpreadProgressText()
        {
            return $"{GetSpreadsRead()}/{GetTotalSpreads()} spreads viewed";
        }
    }
}