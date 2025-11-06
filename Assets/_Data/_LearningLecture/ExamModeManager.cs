using UnityEngine;
using UnityEngine.Playables;
using com.cyborgAssets.inspectorButtonPro;

namespace DreamClass.LearningLecture
{
    public class ExamModeManager : NewMonobehavior
    {
        [SerializeField] private PlayableDirector startAnimation;
        
        private bool isExamActive = false;


        protected override void Start()
        {
            base.Start();
            EndExam();
        }


        /// <summary>
        /// Bắt đầu chế độ Exam - Chạy animation start
        /// </summary>
        [ProButton]
        public void StartExam()
        {
            if (startAnimation == null)
            {
                Debug.LogError("Start Animation is not assigned!");
                return;
            }

            isExamActive = true;
            
            // Reset animation về đầu và chạy
            startAnimation.time = 0;
            startAnimation.Play();
            
            Debug.Log("Exam Started - Animation Playing");
        }

        /// <summary>
        /// Kết thúc Exam - Reset animation về trạng thái ban đầu
        /// </summary>
        [ProButton]
        public void EndExam()
        {
            if (startAnimation == null)
            {
                Debug.LogError("Start Animation is not assigned!");
                return;
            }

            isExamActive = false;
            
            // Dừng animation
            startAnimation.Stop();
            
            // Reset về time = 0 (trạng thái ban đầu)
            startAnimation.time = 0;
            
            // Evaluate để áp dụng trạng thái ban đầu
            startAnimation.Evaluate();
            
            Debug.Log("Exam Ended - Animation Reset to Initial State");
        }

        /// <summary>
        /// Kiểm tra trạng thái Exam
        /// </summary>
        public bool IsExamActive() => isExamActive;


    }
}