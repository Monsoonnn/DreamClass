using DreamClass.QuestSystem.Systems.Quest;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    public abstract class QuestCtrl : NewMonobehavior
    {
        [Header("Quest Info")]
        public string QuestId;
        public string QuestName;
        [TextArea] public string Description;

        [Header("Quest Steps")]
        public List<QuestStep> steps = new List<QuestStep>();

        [Header("Runtime State")]
        public QuestState State = QuestState.NOT_START;

        private int currentStepIndex = 0;
        public bool IsComplete { get; private set; }

        [Header("Reference")]
        private TMP_Text titleText;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadTitleText();
        }

        protected virtual void LoadTitleText() {
            if (titleText != null) return;
            titleText = this.transform.Find("Text")?.GetComponent<TMP_Text>();
            titleText.text = QuestName;
        }



        public void SetState(QuestState newState)
        {
            State = newState;
        }

        public virtual void StartQuest()
        {
            if (State == QuestState.NOT_PREMISE)
            {
                Debug.LogWarning($"[QuestCtrl] Quest '{QuestName}' cannot start yet.");
                return;
            }

            if (steps.Count == 0)
            {
                Debug.LogWarning($"[QuestCtrl] Quest '{QuestName}' has no steps.");
                return;
            }

            IsComplete = false;
            currentStepIndex = 0;
            steps[currentStepIndex].StartStep();
            State = QuestState.IN_PROGRESS;

            Debug.Log($"[QuestCtrl] Started quest: {QuestName}");
        }

        public void UpdateProgress()
        {
            if (State != QuestState.IN_PROGRESS) return;

            QuestStep step = steps[currentStepIndex];

            if (step.IsComplete)
            {
                currentStepIndex++;

                if (currentStepIndex < steps.Count)
                {
                    steps[currentStepIndex].StartStep();
                }
                else
                {
                    _ = CompleteQuest();
                }
            }
        }

        // Class cha - QuestCtrl
        protected virtual async Task CompleteQuest() {
            IsComplete = true;
            State = QuestState.FINISHED;

            try {
                await SyncQuestToServer(); // Update State

                // Hook point cho class con
                await OnBeforeReward();

                await GiveReward();

                // Hook point sau khi trao thưởng
                await OnAfterReward();

                await ShowNotification();

                this.gameObject.SetActive(false);
                Destroy(this.gameObject, 2f);
                Debug.Log($"[QuestCtrl] Quest '{QuestName}' completed!");
            }
            catch (Exception ex) {
                Debug.LogError($"[QuestCtrl] Error: {ex.Message}");
            }
        }

        // Virtual hooks để class con override
        protected virtual async Task OnBeforeReward() {
            await Task.CompletedTask; // Không làm gì trong base class
        }

        protected virtual async Task OnAfterReward() {
            QuestManager.Instance.CompleteQuest(QuestId);
            QuestPermissionManager.Instance.MarkCompleted(QuestId);
            await Task.CompletedTask;
        }

        // Các phương thức hỗ trợ
        private async Task SyncQuestToServer() {
            // Gọi API đồng bộ quest lên server
            // await YourNetworkManager.SyncQuest(questId);
            await Task.Delay(500);
            await Task.CompletedTask; // Placeholder
        }
        private async Task GiveReward() {
            // Logic trao thưởng
            // await RewardManager.GiveReward(rewardId);
            await Task.Delay(500);
            await Task.CompletedTask; // Placeholder
        }
        private async Task ShowNotification() {
            // Hiển thị thông báo
            // await NotificationManager.Show("Quest completed!");
            QuestUIComplete.Instance.UpdateUI(QuestName, "00:00", "50", "1");
           

            await Task.Delay(500);
            await Task.CompletedTask; // Placeholder
        }





    }
}
