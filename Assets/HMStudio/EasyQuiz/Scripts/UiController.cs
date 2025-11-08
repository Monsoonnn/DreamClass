using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HMStudio.EasyQuiz
{
    public class UiController : MonoBehaviour
    {
        [SerializeField] private QuestionManager _questionManager;
        [Space] 
        [SerializeField] private Button _btnNext;
        [SerializeField] private Button _btnPrev;
        [SerializeField] private Button _btnGetPoint;
        [SerializeField] private TextMeshProUGUI _questionStatistic;
        [SerializeField] private TextMeshProUGUI _questionInfo;
        [SerializeField] private TextMeshProUGUI _questionPoint;
        
        [Space]
        [Header("Options")]
        [SerializeField] private Button _btnOptionA;
        [SerializeField] private Button _btnOptionB;
        [SerializeField] private Button _btnOptionC;
        [SerializeField] private Button _btnOptionD;
        
        private void Awake()
        {
            _btnGetPoint.onClick.AddListener(GetPoint);
            _btnNext.onClick.AddListener(NextQuestion);
            _btnPrev.onClick.AddListener(PrevQuestion);
            AnswerQuestion(_btnOptionA);
            AnswerQuestion(_btnOptionB);
            AnswerQuestion(_btnOptionC);
            AnswerQuestion(_btnOptionD);
            ShowStatistic();
        }

        private void AnswerQuestion(Button btnOption)
        {
            btnOption.onClick.AddListener(() =>
            {
                var tmps = btnOption.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (var tmp in tmps)
                {
                    if (tmp.gameObject.name.Contains("tmpAnswer"))
                    {
                        var answerOption = tmp.text.Trim();
                        var isCorrect = _questionManager.AnswerQuestion(answerOption);
                        if (isCorrect)
                        {
                            Debug.LogWarning($"Answer {answerOption} is CORRECT");
                        }
                        else
                        {
                            Debug.LogWarning($"Answer {answerOption} is not CORRECT");
                        }
                    }
                }
                NextQuestion();
            });
        }

        private void Start()
        {
            _questionManager.questionViewer.questionID = 0;
            NextQuestion();
        }

        private void PrevQuestion()
        {
            _questionManager.PrevQuestion();
            ShowStatistic();
        }

        private void NextQuestion()
        {
            _questionManager.NextQuestion();
            ShowStatistic();
        }

        private void ShowStatistic()
        {
            _questionStatistic.SetText(_questionManager.GetStatistic());
            _questionInfo.SetText(_questionManager.GetInfo());
        }

        private void GetPoint()
        {
            _questionPoint.SetText("Point : " + _questionManager.GetPoint());
        }
    }
}