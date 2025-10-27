using UnityEngine;
using System.Collections;

namespace DreamClass.QuestSystem {
    public class QuestUIComplete : SingletonCtrl<QuestUIComplete> {
        [SerializeField] private TMPro.TextMeshProUGUI questName;
        [SerializeField] private TMPro.TextMeshProUGUI timeComplete;
        [SerializeField] private TMPro.TextMeshProUGUI dreamPoint;
        [SerializeField] private TMPro.TextMeshProUGUI ranking;

        protected override void Awake() { 
            base.Awake(); 
            this.gameObject.SetActive(false);
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadQuestName();
            this.LoadTimeComplete();
            this.LoadDreamPoint();
            this.LoadRanking();
        }

        protected virtual void LoadRanking() {
            if (this.ranking != null) return;
            this.ranking = transform.Find("Window/Ranking/Rank").GetComponent<TMPro.TextMeshProUGUI>();
        }

        protected virtual void LoadDreamPoint() {
            if (this.dreamPoint != null) return;
            this.dreamPoint = transform.Find("Window/DreamPoint/Point").GetComponent<TMPro.TextMeshProUGUI>();
        }

        protected virtual void LoadQuestName() {
            if (this.questName != null) return;
            this.questName = transform.Find("Window/QuestName").GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }

        protected virtual void LoadTimeComplete() {
            if (this.timeComplete != null) return;
            this.timeComplete = transform.Find("Window/TimeComplete/Time").GetComponent<TMPro.TextMeshProUGUI>();
        }

        public void UpdateUI( string questName, string timeComplete, string dreamPoint, string ranking ) {
            this.questName.text = "Nhiệm vụ: " + questName;
            this.timeComplete.text = timeComplete;
            this.dreamPoint.text = dreamPoint;
            this.ranking.text = ranking;
            this.gameObject.SetActive(true);

            // Start auto-hide coroutine
            StopAllCoroutines();
            StartCoroutine(HideAfterDelay(3f));
        }

        private IEnumerator HideAfterDelay( float delay ) {
            yield return new WaitForSeconds(delay);
            this.gameObject.SetActive(false);
        }
    }
}
