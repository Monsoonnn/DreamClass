using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Characters.TeacherQuang;
using DreamClass.NPCCore;
using DreamClass.LoginManager;
using TMPro;

namespace Gameplay.Exam {
    /// <summary>
    /// Hệ thống phát thông báo thời gian còn lại của bài thi
    /// Sử dụng NPCManager (TeacherQuang) để phát audio
    /// Ghép câu: "Thời gian còn lại" + "X phút"
    /// </summary>
    public class ExamTimeAnnouncer : NewMonobehavior {
        [Header("References")]
        [SerializeField] private ExamController examController;
        [SerializeField] private TQuangNPCManager npcManager;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private string noExamMessage = "Bạn chưa tham gia bài kiểm tra nào";
        [SerializeField] private string submittingMessage = "Đang nộp...";
        [SerializeField] private string submittedMessage = "Nộp thành công";
        [SerializeField] private float statusMessageDuration = 5f;

        [Header("Announcement Settings")]
        [Tooltip("Các mốc thời gian sẽ thông báo (đơn vị: phút)")]
        [SerializeField] private List<int> announcementMinutes = new List<int> { 10, 8, 5, 3, 2, 1 };
        
        [Tooltip("Có phát thông báo không")]
        [SerializeField] private bool enableAnnouncements = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Theo dõi các mốc đã thông báo
        private HashSet<int> announcedMinutes = new HashSet<int>();
        private bool isSubscribed = false;
        private bool isExamActive = false;

        // Queue để phát audio lần lượt
        private Queue<Func<Task>> audioQueue = new Queue<Func<Task>>();
        private Queue<string> audioQueueTypes = new Queue<string>(); // Track announcement types
        private bool isPlayingAudio = false;
        private Coroutine statusMessageClearCoroutine;

        protected override void LoadComponents() {
            base.LoadComponents();
            LoadExamController();
        }

        private new void Start() {
            base.Start();
            
            // Kiểm tra login trước khi khởi tạo announcer
            if (!IsUserLoggedIn()) {
                UpdateTimeDisplay("Yêu cầu đăng nhập để tham gia bài thi");
                return;
            }
            
            // Hiển thị message mặc định khi chưa tham gia bài kiểm tra
            UpdateTimeDisplay(noExamMessage);
        }

        /// <summary>
        /// Cập nhật hiển thị thời gian trên UI
        /// </summary>
        private void UpdateTimeDisplay(string text) {
            if (timeText != null) {
                timeText.text = text;
            }
        }

        /// <summary>
        /// Cập nhật hiển thị thời gian còn lại (format MM:SS)
        /// </summary>
        private void UpdateTimeDisplay(float remainingSeconds) {
            int mins = (int)(remainingSeconds / 60);
            int secs = (int)(remainingSeconds % 60);
            UpdateTimeDisplay($"{mins:D2}:{secs:D2}");
        }

        protected virtual void LoadExamController() {
            if (examController != null) return;
            examController = GetComponent<ExamController>();
            if (examController == null) {
                examController = FindFirstObjectByType<ExamController>();
            }
        }

        private void OnEnable() {
            SubscribeToExamEvents();
        }

        private void OnDisable() {
            UnsubscribeFromExamEvents();
        }

        private void SubscribeToExamEvents() {
            if (isSubscribed || examController == null) return;
            
            examController.OnTimeUpdated += OnTimeUpdated;
            examController.OnExamStarted += OnExamStarted;
            examController.OnExamFinished += OnExamFinished;
            examController.OnSectionChanged += OnSectionChanged;
            examController.OnFinishExamButtonPressed += OnFinishExamButtonPressed;
            isSubscribed = true;

            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Subscribed to ExamController events");
        }

        private void UnsubscribeFromExamEvents() {
            if (!isSubscribed || examController == null) return;

            examController.OnTimeUpdated -= OnTimeUpdated;
            examController.OnExamStarted -= OnExamStarted;
            examController.OnExamFinished -= OnExamFinished;
            examController.OnSectionChanged -= OnSectionChanged;
            examController.OnFinishExamButtonPressed -= OnFinishExamButtonPressed;
            isSubscribed = false;
        }

        private void OnExamStarted() {
            // Kiểm tra login trước khi bắt đầu thông báo
            if (!IsUserLoggedIn()) {
                if (debugMode)
                    Debug.LogWarning("[ExamTimeAnnouncer] User not logged in - announcements disabled");
                UpdateTimeDisplay("Yêu cầu đăng nhập để tham gia bài thi");
                return;
            }
            
            // Reset các mốc đã thông báo khi bắt đầu bài thi mới
            announcedMinutes.Clear();
            audioQueue.Clear();
            audioQueueTypes.Clear();
            isPlayingAudio = false;
            isExamActive = true;
            
            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Exam started - Reset announcement tracking");

            // Thông báo số phần của bài kiểm tra + section đầu tiên
            EnqueueAnnouncement(() => AnnounceExamIntroAndFirstSection(), "ExamIntro");
        }

        private void OnSectionChanged(int currentIndex, int totalCount) {
            // Thông báo phần tiếp theo (bỏ qua section đầu tiên vì đã đọc trong intro)
            if (currentIndex > 0 && currentIndex < totalCount) {
                var currentSection = examController.CurrentSection;
                if (currentSection != null) {
                    string nextSectionType = currentSection.sectionType == ExamSectionType.Quiz ? "NextQuiz" : "NextExperiment";
                    EnqueueAnnouncement(() => AnnounceNextSection(currentSection.sectionType), nextSectionType);
                }
            }
        }

        private void OnExamFinished() {
            // Phát thông báo hoàn thành ngay lập tức (không qua queue)
            _ = PlayExamCompletedAnnouncement();
            
            isExamActive = false;
            
            // Hiển thị lại message mặc định
            UpdateTimeDisplay(noExamMessage);
            ClearSubmissionStatusMessage();
            
            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Exam finished - playing completion announcement");
        }

        /// <summary>
        /// Phát thông báo hoàn thành bài thi (được gọi từ FinishExam)
        /// Phát trực tiếp, không qua queue
        /// </summary>
        private async Task PlayExamCompletedAnnouncement() {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Playing exam completed announcement: SectionCompleted");

            try {
                // Phát "Chúc mừng bạn đã hoàn thành bài thi"
                await npcManager.characterVoiceline.PlayAnimation(TeacherQuang.SectionCompleted, true);

                if (debugMode)
                    Debug.Log("[ExamTimeAnnouncer] Finished playing exam completed announcement");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error playing exam completed announcement: {ex.Message}");
            }
        }
        
        private IEnumerator ClearQueueAfterDelay(float delay) {
            yield return new WaitForSeconds(delay);
            audioQueue.Clear();
            audioQueueTypes.Clear();
            isPlayingAudio = false;
        }

        private void OnFinishExamButtonPressed() {
            // Hiển thị "Đang nộp..." ngay lập tức
            ShowSubmissionStatus(submittingMessage);
            
            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Finish exam button pressed - showing submission status");
            
            // Schedule để hiển thị "Nộp thành công" sau khi xử lý xong
            StopSubmissionStatusTimer();
            statusMessageClearCoroutine = StartCoroutine(DelayShowSuccessMessage());
        }

        private IEnumerator DelayShowSuccessMessage() {
            // Chờ 0.5s để "Đang nộp..." hiển thị, rồi chuyển sang "Nộp thành công"
            yield return new WaitForSeconds(0.5f);
            ShowSubmissionStatus(submittedMessage);
            
            // Chờ 5s rồi xóa message
            yield return new WaitForSeconds(statusMessageDuration);
            ClearSubmissionStatusMessage();
            
            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Submission status message cleared");
        }

        private void OnTimeUpdated(float remainingSeconds) {
            // Cập nhật UI thời gian
            if (isExamActive) {
                UpdateTimeDisplay(remainingSeconds);
            }

            // Kiểm tra login trước khi phát thông báo
            if (!IsUserLoggedIn()) {
                if (debugMode)
                    Debug.LogWarning("[ExamTimeAnnouncer] User not logged in - time announcements disabled");
                return;
            }

            if (!enableAnnouncements) return;
            if (npcManager == null || npcManager.characterVoiceline == null) return;

            int remainingMinutes = Mathf.CeilToInt(remainingSeconds / 60f);

            // Kiểm tra xem có đến mốc thông báo chưa
            foreach (int minute in announcementMinutes) {
                // Kiểm tra điều kiện: 
                // - Thời gian còn lại <= mốc thông báo
                // - Chưa thông báo mốc này
                // - Thời gian còn lại > mốc tiếp theo (để không thông báo nhiều lần)
                if (remainingMinutes == minute && !announcedMinutes.Contains(minute)) {
                    // Đánh dấu đã thông báo
                    announcedMinutes.Add(minute);
                    
                    // Thêm vào queue để phát lần lượt
                    EnqueueAnnouncement(() => AnnounceTimeRemaining(minute), $"TimeRemaining_{minute}");
                    break;
                }
            }
        }

        #region Audio Queue System

        /// <summary>
        /// Thêm announcement vào queue và xử lý (không cho phép trùng lặp)
        /// </summary>
        private void EnqueueAnnouncement(Func<Task> announcement, string announcementType = "") {
            // Kiểm tra xem announcement type này đã có trong queue chưa
            if (!string.IsNullOrEmpty(announcementType) && audioQueueTypes.Contains(announcementType)) {
                if (debugMode)
                    Debug.Log($"[ExamTimeAnnouncer] Skipping duplicate announcement: {announcementType}");
                return;
            }

            audioQueue.Enqueue(announcement);
            if (!string.IsNullOrEmpty(announcementType)) {
                audioQueueTypes.Enqueue(announcementType);
            }
            
            if (!isPlayingAudio) {
                _ = ProcessAudioQueue();
            }
        }

        /// <summary>
        /// Xử lý queue audio - phát lần lượt từng announcement
        /// </summary>
        private async Task ProcessAudioQueue() {
            if (isPlayingAudio) return;
            
            isPlayingAudio = true;

            while (audioQueue.Count > 0) {
                var announcement = audioQueue.Dequeue();
                
                // Remove corresponding type from queue
                if (audioQueueTypes.Count > 0) {
                    var announcementType = audioQueueTypes.Dequeue();
                    if (debugMode && !string.IsNullOrEmpty(announcementType))
                        Debug.Log($"[ExamTimeAnnouncer] Playing announcement: {announcementType}");
                }
                
                try {
                    await announcement();
                }
                catch (Exception ex) {
                    Debug.LogError($"[ExamTimeAnnouncer] Error processing audio queue: {ex.Message}");
                }
            }

            isPlayingAudio = false;
        }

        #endregion

        /// <summary>
        /// Phát thông báo thời gian còn lại
        /// Ghép: "Thời gian còn lại" + "X phút"
        /// </summary>
        private async Task AnnounceTimeRemaining(int minutes) {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            if (debugMode)
                Debug.Log($"[ExamTimeAnnouncer] Announcing: Thời gian còn lại {minutes} phút");

            try {
                // Bước 1: Phát "Thời gian còn lại"
                await npcManager.characterVoiceline.PlayAnimation(TeacherQuang.TimeRemaining, true);

                // Bước 2: Phát số phút tương ứng
                TeacherQuang minuteVoice = GetMinuteVoiceline(minutes);
                await npcManager.characterVoiceline.PlayAnimation(minuteVoice, true);

                if (debugMode)
                    Debug.Log($"[ExamTimeAnnouncer] Finished announcing {minutes} minutes");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error announcing time: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy enum voiceline tương ứng với số phút
        /// </summary>
        private TeacherQuang GetMinuteVoiceline(int minutes) {
            return minutes switch {
                1 => TeacherQuang.Minute_1,
                2 => TeacherQuang.Minute_2,
                5 => TeacherQuang.Minute_5,
                8 => TeacherQuang.Minute_8,
                10 => TeacherQuang.Minute_10,
                _ => TeacherQuang.Minute_1 // Fallback
            };
        }

        /// <summary>
        /// Lấy enum voiceline tương ứng với số phần
        /// </summary>
        private TeacherQuang GetPartVoiceline(int partCount) {
            return partCount switch {
                1 => TeacherQuang.Part_1,
                2 => TeacherQuang.Part_2,
                3 => TeacherQuang.Part_3,
                4 => TeacherQuang.Part_4,
                5 => TeacherQuang.Part_5,
                _ => TeacherQuang.Part_1 // Fallback
            };
        }

        /// <summary>
        /// Thông báo giới thiệu bài kiểm tra + section đầu tiên
        /// Ghép: "Bài kiểm tra này gồm" + "X phần" + "Phần đầu tiên là trắc nghiệm/thực hành"
        /// </summary>
        private async Task AnnounceExamIntroAndFirstSection() {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            int totalSections = examController.TotalSections;
            if (totalSections <= 0 || totalSections > 5) {
                Debug.LogWarning($"[ExamTimeAnnouncer] Invalid section count: {totalSections}");
                return;
            }

            try {
                // Bước 1: Phát "Bài kiểm tra này gồm"
                if (debugMode)
                    Debug.Log($"[ExamTimeAnnouncer] Announcing: Bài kiểm tra này gồm {totalSections} phần");
                    
                await npcManager.characterVoiceline.PlayAnimation(TeacherQuang.ExamContains, true);

                // Bước 2: Phát số phần tương ứng
                TeacherQuang partVoice = GetPartVoiceline(totalSections);
                await npcManager.characterVoiceline.PlayAnimation(partVoice, true);

                // Bước 3: Hướng dẫn nộp bài
                await AnnounceSubmissionInstructions();

                // Bước 4: Phát section đầu tiên
                var firstSection = examController.CurrentSection;
                if (firstSection != null) {
                    string sectionName = firstSection.sectionType == ExamSectionType.Quiz ? "trắc nghiệm" : "thực hành";
                    if (debugMode)
                        Debug.Log($"[ExamTimeAnnouncer] Announcing first section: {sectionName}");

                    TeacherQuang sectionVoice = firstSection.sectionType == ExamSectionType.Quiz 
                        ? TeacherQuang.NextSectionQuiz 
                        : TeacherQuang.NextSectionExperiment;
                    await npcManager.characterVoiceline.PlayAnimation(sectionVoice, true);
                }

                if (debugMode)
                    Debug.Log($"[ExamTimeAnnouncer] Finished announcing exam intro and first section");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error announcing exam intro: {ex.Message}");
            }
        }

        /// <summary>
        /// Thông báo giới thiệu bài kiểm tra
        /// Ghép: "Bài kiểm tra này gồm" + "X phần"
        /// </summary>
        private async Task AnnounceExamIntro() {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            int totalSections = examController.TotalSections;
            if (totalSections <= 0 || totalSections > 5) {
                Debug.LogWarning($"[ExamTimeAnnouncer] Invalid section count: {totalSections}");
                return;
            }

            if (debugMode)
                Debug.Log($"[ExamTimeAnnouncer] Announcing: Bài kiểm tra này gồm {totalSections} phần");

            try {
                // Bước 1: Phát "Bài kiểm tra này gồm"
                await npcManager.characterVoiceline.PlayAnimation(TeacherQuang.ExamContains, true);

                // Bước 2: Phát số phần tương ứng
                TeacherQuang partVoice = GetPartVoiceline(totalSections);
                await npcManager.characterVoiceline.PlayAnimation(partVoice, true);

                if (debugMode)
                    Debug.Log($"[ExamTimeAnnouncer] Finished announcing exam intro");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error announcing exam intro: {ex.Message}");
            }
        }

        /// <summary>
        /// Thông báo phần tiếp theo
        /// Phát 1 audio: "Phần kế tiếp là trắc nghiệm" hoặc "Phần kế tiếp là thực hành"
        /// </summary>
        private async Task AnnounceNextSection(ExamSectionType sectionType) {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            string sectionName = sectionType == ExamSectionType.Quiz ? "trắc nghiệm" : "thực hành";
            if (debugMode)
                Debug.Log($"[ExamTimeAnnouncer] Announcing: Phần kế tiếp là {sectionName}");

            try {
                // Phát audio đầy đủ "Phần kế tiếp là trắc nghiệm/thực hành"
                TeacherQuang sectionVoice = sectionType == ExamSectionType.Quiz 
                    ? TeacherQuang.NextSectionQuiz 
                    : TeacherQuang.NextSectionExperiment;
                await npcManager.characterVoiceline.PlayAnimation(sectionVoice, true);

                if (debugMode)
                    Debug.Log($"[ExamTimeAnnouncer] Finished announcing next section");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error announcing next section: {ex.Message}");
            }
        }


        /// <summary>
        /// Thêm mốc thời gian cần thông báo
        /// </summary>
        public void AddAnnouncementMinute(int minute) {
            if (!announcementMinutes.Contains(minute)) {
                announcementMinutes.Add(minute);
                announcementMinutes.Sort((a, b) => b.CompareTo(a)); // Sort descending
            }
        }

        /// <summary>
        /// Xóa mốc thời gian
        /// </summary>
        public void RemoveAnnouncementMinute(int minute) {
            announcementMinutes.Remove(minute);
        }

        /// <summary>
        /// Reset tracking để có thể thông báo lại
        /// </summary>
        public void ResetAnnouncementTracking() {
            announcedMinutes.Clear();
        }

        /// <summary>
        /// Bật/tắt thông báo
        /// </summary>
        public void SetAnnouncementsEnabled(bool enabled) {
            enableAnnouncements = enabled;
        }

        /// <summary>
        /// Thông báo chúc mừng hoàn thành phần thi
        /// Phát khi chuyển từ section này sang section khác
        /// </summary>
        private async Task AnnounceSectionCompleted() {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Announcing: Chúc mừng bạn đã hoàn thành phần thi");

            try {
                // Phát "Chúc mừng bạn đã hoàn thành phần thi" (fallback to _100Pass if SectionCompleted not available)
                await npcManager.characterVoiceline.PlayAnimation(TeacherQuang._100Pass, true);

                if (debugMode)
                    Debug.Log("[ExamTimeAnnouncer] Finished announcing section completed");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error announcing section completed: {ex.Message}");
            }
        }

        /// <summary>
        /// Hướng dẫn nộp bài
        /// Phát sau khi thông báo "Bài kiểm tra này gồm X phần"
        /// </summary>
        private async Task AnnounceSubmissionInstructions() {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Announcing: Hướng dẫn nộp bài");

            try {
                // Phát hướng dẫn nộp bài (fallback to WelcomeToTesting if SubmissionInstructions not available)
                await npcManager.characterVoiceline.PlayAnimation(TeacherQuang.SubmissionInstructions, true);

                if (debugMode)
                    Debug.Log("[ExamTimeAnnouncer] Finished announcing submission instructions");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error announcing submission instructions: {ex.Message}");
            }
        }

        /// <summary>
        /// Test phát thông báo (dùng trong Editor)
        /// </summary>
        [ContextMenu("Test Announce 5 Minutes")]
        public void TestAnnounce5Minutes() {
            _ = AnnounceTimeRemaining(5);
        }

        [ContextMenu("Test Announce 1 Minute")]
        public void TestAnnounce1Minute() {
            _ = AnnounceTimeRemaining(1);
        }

        [ContextMenu("Test Section Completed")]
        public void TestAnnounceSectionCompleted() {
            _ = AnnounceSectionCompleted();
        }

        [ContextMenu("Test Submission Instructions")]
        public void TestAnnounceSubmissionInstructions() {
            _ = AnnounceSubmissionInstructions();
        }

        /// <summary>
        /// Phát thông báo nhắc nhở khi đang trong bài thi
        /// </summary>
        public async Task AnnounceWarningDuringTest() {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[ExamTimeAnnouncer] NPCManager or VoicelineManager is null!");
                return;
            }

            if (debugMode)
                Debug.Log("[ExamTimeAnnouncer] Announcing: Warning during test");

            try {
                await npcManager.characterVoiceline.PlayAnimation(TeacherQuang.WarningDuringTest, true);
            }
            catch (System.Exception ex) {
                Debug.LogError($"[ExamTimeAnnouncer] Error announcing warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Hiển thị submission status message
        /// </summary>
        private void ShowSubmissionStatus(string message) {
            if (timeText != null) {
                timeText.text = message;
            }
            
            if (debugMode)
                Debug.Log($"[ExamTimeAnnouncer] Submission Status: {message}");
        }

        /// <summary>
        /// Xóa submission status message
        /// </summary>
        private void ClearSubmissionStatusMessage() {
            if (timeText != null) {
                timeText.text = noExamMessage;
            }
        }

        /// <summary>
        /// Kiểm tra xem user đã login chưa
        /// </summary>
        private bool IsUserLoggedIn() {
            if (LoginManager.Instance == null) {
                if (debugMode)
                    Debug.LogError("[ExamTimeAnnouncer] LoginManager.Instance is null!");
                return false;
            }

            return LoginManager.Instance.IsLoggedIn();
        }

        /// <summary>
        /// Kích hoạt announcer sau khi user đã login
        /// Có thể được gọi từ LoginManager sau khi login thành công
        /// </summary>
        public void ActivateAnnouncerAfterLogin() {
            if (!IsUserLoggedIn()) {
                Debug.LogWarning("[ExamTimeAnnouncer] Cannot activate announcer - user not logged in");
                return;
            }

            Debug.Log("[ExamTimeAnnouncer] Activating announcer after successful login");
            
            // Hiển thị lại message mặc định
            UpdateTimeDisplay(noExamMessage);
            
            // Kích hoạt lại announcements
            enableAnnouncements = true;
        }

        /// <summary>
        /// Dừng timer cho submission status message
        /// </summary>
        private void StopSubmissionStatusTimer() {
            if (statusMessageClearCoroutine != null) {
                StopCoroutine(statusMessageClearCoroutine);
                statusMessageClearCoroutine = null;
            }
        }
    }
}
