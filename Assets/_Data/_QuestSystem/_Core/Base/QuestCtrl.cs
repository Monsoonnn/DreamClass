using AudioManager;
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

        public int currentStepIndex = 0;


        public bool IsComplete { get; private set; }



        [Header("Reference")]
        private TMP_Text titleText;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadTitleText();
        }

        protected virtual void LoadTitleText()
        {
            if (titleText != null) return;
            titleText = this.transform.Find("Text")?.GetComponent<TMP_Text>();
            titleText.text = QuestName;
        }



        public void SetState(QuestState newState)
        {
            State = newState;
        }
        public void SetComplete(bool isComplete)
        {
            IsComplete = isComplete;
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
            State = QuestState.IN_PROGRESS;

            // Start quest on server if using Server API and logged in
            if (QuestManager.Instance != null && QuestManager.Instance.IsUsingServerAPI)
            {
                StartCoroutine(StartQuestOnServerAsync());
            }

            // Hide UICanvas from NPCManager holder before starting animation
            HideNPCUICanvas();

            steps[currentStepIndex].StartStep();
        }

        /// <summary>
        /// Hide UICanvas from NPC that holds this quest
        /// Only works for QuestType1
        /// </summary>
        protected virtual void HideNPCUICanvas()
        {
            // Only hide UICanvas for QuestType1
            if (!(this is QuestType1)) return;

            // Find parent NPC that has NPCManager
            DreamClass.NPCCore.NPCManager npcManager = transform.parent?.GetComponent<DreamClass.NPCCore.NPCManager>();
            if (npcManager != null)
            {
                npcManager.SetUICanvasActive(false);
                Debug.Log($"[QuestCtrl] Hidden UICanvas for quest '{QuestName}'");
            }
        }

        /// <summary>
        /// Show UICanvas from NPC that holds this quest
        /// Only works for QuestType1
        /// </summary>
        public virtual void ShowNPCUICanvas()
        {
            // Only show UICanvas for QuestType1
            if (!(this is QuestType1)) return;

            DreamClass.NPCCore.NPCManager npcManager = transform.parent?.GetComponent<DreamClass.NPCCore.NPCManager>();
            if (npcManager != null)
            {
                npcManager.SetUICanvasActive(true);
                Debug.Log($"[QuestCtrl] Showed UICanvas for quest '{QuestName}'");
            }
        }

        private System.Collections.IEnumerator StartQuestOnServerAsync()
        {
            // Gọi QuestManager để start quest trên server
            // QuestManager sẽ handle việc gọi StartQuestOnServer() API
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestCtrl] ApiClient not found.");
                yield break;
            }

            string endpoint = $"/api/quests/my-quests/{QuestId}/start";
            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "POST");

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            if (response != null && response.IsSuccess)
            {
                Debug.Log($"[QuestCtrl] Quest '{QuestName}' synchronized with server successfully");
            }
            else
            {
                Debug.LogError($"[QuestCtrl] Failed to sync quest with server: {response?.Error}");
            }
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
                    Debug.LogWarning($"[QuestCtrl] Step {step.StepId} completed!");
                    steps[currentStepIndex].StartStep();
                }
                else
                {
                    _ = CompleteQuest();
                }
            }
        }

        // Class cha - QuestCtrl
        protected virtual async Task CompleteQuest()
        {
            IsComplete = true;
            State = QuestState.FINISHED;

            try
            {

                var (isSuccess, questData) = await WaitForServerConfirmation();

                if (isSuccess && questData != null)
                {
                    Debug.Log($"[QuestCtrl] QuestData: {JsonUtility.ToJson(questData)}");
                    //await SyncQuestToServer();
                    //await OnBeforeReward();
                    await ShowNotification(questData);
                    await GiveReward();
                    await OnAfterReward();

                    this.gameObject.SetActive(false);
                    Destroy(this.gameObject, 2f);
                }
                else
                {
                    Debug.LogWarning($"[QuestCtrl] Quest completion failed on server. Reactivating quest '{QuestName}'...");
                    IsComplete = false;
                    State = QuestState.NOT_START;
                    this.gameObject.SetActive(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestCtrl] Error: {ex.Message}");
            }
        }

        private async Task<(bool success, QuestDataJson data)> WaitForServerConfirmation()
        {
            bool? result = null;
            QuestDataJson questData = null;

            QuestManager.Instance.CompleteQuest(QuestId, (success, data) =>
            {
                result = success;
                questData = data;
            });

            // Wait for server response (max 10 seconds)
            float timeout = 10f;
            float elapsed = 0f;
            while (result == null && elapsed < timeout)
            {
                await Task.Delay(100);
                elapsed += 0.1f;
            }

            if (result == null)
            {
                Debug.LogError("[QuestCtrl] Server confirmation timeout!");
                return (false, null);
            }

            return (result.Value, questData);
        }

        // Virtual hooks để class con override
        protected virtual async Task OnBeforeReward()
        {
            await Task.CompletedTask; // Không làm gì trong base class
        }

        protected virtual async Task OnAfterReward()
        {
            // Note: CompleteQuest() is now called in WaitForServerConfirmation()
            QuestPermissionManager.Instance.MarkCompleted(QuestId);
            await Task.CompletedTask;
        }

        // Các phương thức hỗ trợ
        private async Task SyncQuestToServer()
        {
            // Gọi API đồng bộ quest lên server
            // await YourNetworkManager.SyncQuest(questId);
            await Task.Delay(500);
            await Task.CompletedTask; // Placeholder
        }
        private async Task GiveReward()
        {
            // Logic trao thưởng
            // await RewardManager.GiveReward(rewardId);
            await Task.Delay(500);
            await Task.CompletedTask; // Placeholder
        }
        private async Task ShowNotification(QuestDataJson questData)
        {
            string completedTime = "";
            int rewardGold = 0;

            if (questData != null)
            {
                rewardGold = questData.gold;
                if (!string.IsNullOrEmpty(questData.completedAt))
                {
                    if (DateTime.TryParse(questData.completedAt, out DateTime parsedDate))
                    {
                        // completedTime = parsedDate.ToString("dd/MM/yyyy HH:mm:ss");
                        // Hoặc các format khác:
                        completedTime = parsedDate.ToString("dd/MM/yyyy");
                        // completedTime = parsedDate.ToString("dd-MM-yyyy HH:mm");
                        // completedTime = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[QuestCtrl] No quest data available for notification");
            }

            // Hiển thị UI với dữ liệu từ API
            QuestUIComplete.Instance.UpdateUI(QuestName, completedTime, rewardGold.ToString(), "1");
            SFXManager.Instance.PlaySFXByID("SFX001");

            await Task.Delay(500);
        }


        public string GetCurrentStep()
        {
            if (currentStepIndex < steps.Count)
            {
                return steps[currentStepIndex].StepId;
            }
            return null;
        }



    }
}
