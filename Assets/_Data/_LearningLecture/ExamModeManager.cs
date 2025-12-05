using UnityEngine;
using UnityEngine.Playables;
using com.cyborgAssets.inspectorButtonPro;
using HMStudio.EasyQuiz;  // Import namespace quiz
using Characters.TeacherQuang;
using DreamClass.NPCCore;

namespace DreamClass.LearningLecture
{
    public class ExamModeManager : NewMonobehavior
    {
        [SerializeField] private PlayableDirector startAnimation;
        [SerializeField] private QuestionManager questionManager;

        [SerializeField] private ExamUIManager examUIManager;

        [SerializeField] private GameObject quizObject;
        
        [SerializeField] private NPCManager npcManager;

        private bool isExamActive = false;

        private Quaternion rotaionAnimtion;

        protected override void Start()
        {
            base.Start();
            EndExam();
            RestartAnimation(); 

            // Subscribe event hoàn thành quiz
            if (questionManager != null)
            {
                questionManager.OnQuizComplete += ReceiveQuizResult;
                questionManager.OnAPIQuizSubmitted += ReceiveAPIQuizResult;
            }
        }

        [ProButton]
        public void StartExam()
        {
            if (startAnimation == null)
            {
                Debug.LogError("Start Animation is not assigned!");
                return;
            }

            isExamActive = true;
            
            // Reset và play animation
            startAnimation.time = 0;
            startAnimation.Play();
            
            // Chờ animation hoàn thành rồi start quiz
            startAnimation.stopped += OnStartAnimationCompleted;
            
            Debug.Log("Exam Started - Animation Playing");
        }

        private void OnStartAnimationCompleted(PlayableDirector director)
        {
            startAnimation.stopped -= OnStartAnimationCompleted;
            npcManager.CharacterVoiceline.PlayAnimation(TeacherQuang.Call.ToString(), true);
            rotaionAnimtion = npcManager.Model.rotation;
        }


        [ProButton]
        public void StartQuiz()
        {
             if (questionManager != null)
            {
                quizObject.SetActive(true);
                questionManager.StartQuiz();  
            }
            else
            {
                Debug.LogError("QuestionManager not assigned!");
            }
        }

        [ProButton]
        public void EndExam()
        {

            quizObject.SetActive(false);
            examUIManager.HideAllPanels();
            // npcManager.ResetRotation();

            Debug.Log("Exam Ended - Animation Reset to Initial State");
        }

        public bool IsExamActive() => isExamActive;


        private void RestartAnimation()
        {
            if (startAnimation == null)
            {
                Debug.LogError("Start Animation is not assigned!");
                return;
            }

            isExamActive = false;
            
            startAnimation.Stop();
            startAnimation.time = 0;
            startAnimation.Evaluate();
        }
        // Xử lý result từ quiz (Excel mode - dùng local score)
        private void ReceiveQuizResult(float score)
        {
            Debug.Log($"Quiz Completed! Score: {score:P2}");  

            npcManager.LookAtPlayer();

            if (score < 0.7f)
            {
                npcManager.CharacterVoiceline.PlayAnimation(TeacherQuang.Fail.ToString(), true);
            }
            else if (score < 1f)
            {
                npcManager.CharacterVoiceline.PlayAnimation(TeacherQuang._70Pass.ToString(), true);
            }
            else
            {
                npcManager.CharacterVoiceline.PlayAnimation(TeacherQuang._100Pass.ToString(), true);
            }

            EndExam();  // Tự động end exam sau khi có result
        }

        // Xử lý result từ API (API mode - dùng server response)
        private void ReceiveAPIQuizResult(bool success, QuizSubmitResult result)
        {
            if (!success || result == null)
            {
                Debug.LogWarning("[ExamModeManager] API submit failed, using local result");
                return; // OnQuizComplete sẽ được gọi với local score
            }

            Debug.Log($"[ExamModeManager] API Quiz Result: {result.correctCount}/{result.totalQuestions} (Score: {result.score})");

            npcManager.LookAtPlayer();

            // Tính tỉ lệ đúng từ API response
            float percentage = result.GetPercentage();

            if (percentage < 0.7f)
            {
                npcManager.CharacterVoiceline.PlayAnimation(TeacherQuang.Fail.ToString(), true);
                Debug.Log($"[ExamModeManager] Result: FAIL ({result.correctCount}/{result.totalQuestions} = {percentage:P0})");
            }
            else if (percentage < 1f)
            {
                npcManager.CharacterVoiceline.PlayAnimation(TeacherQuang._70Pass.ToString(), true);
                Debug.Log($"[ExamModeManager] Result: 70% PASS ({result.correctCount}/{result.totalQuestions} = {percentage:P0})");
            }
            else
            {
                npcManager.CharacterVoiceline.PlayAnimation(TeacherQuang._100Pass.ToString(), true);
                Debug.Log($"[ExamModeManager] Result: 100% PASS ({result.correctCount}/{result.totalQuestions} = {percentage:P0})");
            }

            EndExam();  // Tự động end exam sau khi có result
        }

        protected virtual void OnDestroy()
        {
            if (questionManager != null)
            {
                questionManager.OnQuizComplete -= ReceiveQuizResult;
                questionManager.OnAPIQuizSubmitted -= ReceiveAPIQuizResult;
            }
        }
    }
}