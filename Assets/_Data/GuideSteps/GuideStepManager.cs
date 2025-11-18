using UnityEngine;
using TMPro;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using System.Linq;
using System;
using System.Collections;
using DreamClass.QuestSystem;
using playerCtrl;
using Unity.VisualScripting;

/// <summary>
/// Controls the logic flow of guide steps across multiple guides.
/// Quản lý Quest hiện tại và xử lý restart/abandon
/// </summary>
public class GuideStepManager : SingletonCtrl<GuideStepManager>
{
    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [SerializeField] List<GameController> gameControllerList = new List<GameController>();
    public List<GameController> GameControllerList => gameControllerList;

    [Header("Active Game")]
    [SerializeField] public GameController gameController;

    [Header("Current Quest")]
    [SerializeField] private QuestType2 currentQuest; // Quest đang active
    public QuestType2 CurrentQuest => currentQuest;

    [Header("Guides (Asset References)")]
    [Tooltip("All available guide ScriptableObjects")]
    public List<GuideData> allGuides = new List<GuideData>();

    [Header("Runtime Info (Debug Only)")]
    public string currentGuideID;
    public string currentStepID;
    public string currentStatus;

    // Internal runtime data
    private GuideData currentGuideAsset;
    private GuideData currentGuideRuntime;
    public GuideData CurrentGuideRuntime => currentGuideRuntime;
    private StepData currentStep;
    private int currentStepIndex = -1;

    private Coroutine coroutine = null;

    // ===== EVENTS =====
    public event Action<string> OnStepCompleted;
    public event Action<string> OnStepActivated;
    public event Action<string> OnGuideStarted;
    public event Action<string> OnGuideFinished;

    // ===== QUEST MANAGEMENT =====

    /// <summary>
    /// Set quest hiện tại
    /// Nếu quest cũ chưa hoàn thành -> Restart và cảnh báo
    /// </summary>
    public void SetCurrentQuest(QuestType2 newQuest)
    {
        // Kiểm tra nếu có quest cũ đang chạy
        if (currentQuest != null && currentQuest != newQuest)
        {
            // Check xem quest cũ đã hoàn thành chưa
            if (!IsCurrentQuestCompleted())
            {
                RestartQuest();
                AbandonQuest();
            }
        }

        // Set quest mới
        currentQuest = newQuest;

        Debug.Log($"[GuideStepManager] Current quest set to: {newQuest?.gameObject.name}");
    }

    /// <summary>
    /// HÀM 1: RESTART QUEST
    /// - Reset experiment về trạng thái ban đầu
    /// - Reset guide về bước đầu
    /// - Quest vẫn active
    /// </summary>
    [ProButton]
    public void RestartQuest()
    {
        if (currentQuest == null)
        {
            Debug.LogWarning("[GuideStepManager] No current quest to restart!");
            return;
        }

        Debug.Log($"[GuideStepManager] ===== RESTARTING QUEST: {currentQuest.gameObject.name} =====");

        // 1. Reset Experiment
        var experimentController = currentQuest.GetExperimentController();
        if (experimentController != null)
        {
            experimentController.ResetExperiment();
            experimentController.SetupExperiment();
        }

        // 2. Reset Guide về bước đầu
        RestartGuide();

        // 3. Thông báo cho quest biết đã restart
        currentQuest.SendMessage("OnQuestRestarted", SendMessageOptions.DontRequireReceiver);

        Debug.Log($"[GuideStepManager] Quest restarted successfully");
    }

    /// <summary>
    /// HÀM 2: ABANDON QUEST (TỪ BỎ QUEST)
    /// - Reset toàn bộ
    /// - SetActive(false) GameObject quest
    /// - Clear current quest reference
    /// </summary>
    [ProButton]
    public void AbandonQuest()
    {
        if (currentQuest == null)
        {
            Debug.LogWarning("[GuideStepManager] No current quest to abandon!");
            return;
        }

        // Check xem quest cũ đã hoàn thành chưa
        if (!IsCurrentQuestCompleted())
        {
            // Cảnh báo quest cũ chưa hoàn thành
            string questName = currentQuest.gameObject.name;
            VRAlertInstance.Instance?.CreateAlerts(new List<string> {
                    $"Chưa hoàn thành: \n{questName}"
                }, "Bạn đang từ bỏ nhiệm vụ sau", "Từ bỏ nhiệm vụ sẽ không lưu tiến độ");

            Debug.LogWarning($"[GuideStepManager] Quest '{questName}' not completed yet!");

            // Restart quest cũ
            RestartQuest();
        }


        Debug.Log($"[GuideStepManager] ===== ABANDONING QUEST: {currentQuest.gameObject.name} =====");

        // 1. Stop experiment
        var experimentController = currentQuest.GetExperimentController();
        if (experimentController != null)
        {
            if (experimentController.IsExperimentRunning())
            {
                experimentController.StopExperiment();
            }
            experimentController.StopAllTracking();
            experimentController.transform.parent.gameObject.SetActive(false);
        }

        // 2. Reset guide
        RestartGuide();
        RestartTitle(0f);

        // 3. Deactivate quest GameObject
        GameObject questObject = currentQuest.gameObject;
        currentQuest = null; // Clear reference trước khi deactivate
        questObject.SetActive(true);

        Debug.Log($"[GuideStepManager] Quest abandoned and deactivated");
    }

    /// <summary>
    /// Kiểm tra quest hiện tại đã hoàn thành chưa
    /// </summary>
    public bool IsCurrentQuestCompleted()
    {
        if (currentQuest == null) return true;

        // Check tất cả steps đã completed chưa
        if (currentGuideRuntime != null && currentGuideRuntime.steps.Count > 0)
        {
            return currentGuideRuntime.steps.All(step => step.isCompleted);
        }

        return false;
    }

    // ===== ORIGINAL METHODS =====

    protected override void LoadComponents()
    {
        base.LoadComponents();
        this.LoadAllGameplay();
    }

    [ProButton]
    protected virtual void LoadAllGameplay()
    {
        if (gameControllerList != null && gameControllerList.Count > 0 && gameControllerList.Any(g => g != null)) return;
        gameControllerList = FindObjectsByType<GameController>(FindObjectsSortMode.None).ToList();
    }

    protected override void Start()
    {
        // Auto-load first guide for testing if needed
    }

    [ProButton]
    public virtual void LoadGameplay(string id)
    {
        GameController tempGc = null;
        foreach (GameController gc in gameControllerList)
        {
            if (gc.GetExperimentName() == id) tempGc = gc;
        }
        if (tempGc != null)
        {
            if (gameController != null)
            {
                gameController.transform.parent.gameObject.SetActive(false);
                gameController.OnDisableGame();
            }
            tempGc.transform.parent.gameObject.SetActive(true);
            tempGc.OnActiveGame();
        }
        else Debug.LogWarning("[GuideStepManager] No game found with ID: " + id);
    }

    [ProButton]
    public void SetCurrentGuide(string guideID)
    {
        GuideData found = allGuides.Find(g => g.guideID == guideID);
        if (found == null)
        {
            Debug.LogWarning($"[GuideStepManager] Guide '{guideID}' not found!");
            return;
        }

        currentGuideRuntime = Instantiate(found);
        currentGuideAsset = found;
        currentGuideID = guideID;

        Debug.Log($"[GuideStepManager] Switched to guide: {guideID}");
        OnGuideStarted?.Invoke(guideID);

        if (currentGuideRuntime.steps.Count > 0)
            ActivateStep(0);
    }
    [ProButton]
    public void ActivateStep(int index)
    {
        if (currentGuideRuntime == null || currentGuideRuntime.steps.Count == 0)
        {
            Debug.LogWarning("[GuideStepManager] No active guide!");
            return;
        }

        if (index < 0 || index >= currentGuideRuntime.steps.Count)
        {
            Debug.LogWarning("[GuideStepManager] Invalid step index!");
            return;
        }

        int rollbackIndex = FindLastUncompletedStepBefore(index);
        if (rollbackIndex != -1 && rollbackIndex < index)
        {
            Debug.LogWarning($"[GuideStepManager] Cannot activate step {index}, previous step {rollbackIndex} not completed! Rolling back...");
            ActivateStep(rollbackIndex);
            return;
        }

        StepData step = currentGuideRuntime.steps[index];
        currentStep = step;
        currentStepIndex = index;
        currentStepID = step.stepID;
        step.isCompleted = false;

        if (titleText != null) titleText.text = step.title;
        if (descriptionText != null) descriptionText.text = step.description;

        if (step.highlightTarget != null)
            Debug.Log($"Highlighting target: {step.highlightTarget.name}");

        currentStatus = $"{currentGuideID} / Step {index + 1}: {step.stepID}";
        Debug.Log($"[GuideStepManager] Activated step: {step.stepID}");

        OnStepActivated?.Invoke(step.stepID);
    }

    [ProButton]
    public void ActivateStep(string stepID)
    {
        if (currentGuideRuntime == null || currentGuideRuntime.steps.Count == 0)
        {
            Debug.LogWarning("[GuideStepManager] No active guide!");
            return;
        }

        int index = currentGuideRuntime.steps.FindIndex(s => s.stepID == stepID);
        if (index == -1)
        {
            Debug.LogWarning($"[GuideStepManager] Step '{stepID}' not found in guide '{currentGuideID}'!");
            return;
        }

        ActivateStep(index);
    }

    [ProButton]
    public void CompleteStep(string stepID)
    {
        if (currentStep == null)
        {
            Debug.LogWarning("[GuideStepManager] No active step!");
            return;
        }

        if (currentStep.stepID != stepID)
        {
            Debug.LogWarning($"[GuideStepManager] Tried to complete '{stepID}', but current step is '{currentStep.stepID}'!");
            return;
        }

        currentStep.isCompleted = true;
        Debug.Log($"[GuideStepManager] Step completed: {currentStep.stepID}");

        OnStepCompleted?.Invoke(stepID);

        int nextIndex = FindNextUncompletedStepAfter(currentStepIndex);

        if (nextIndex != -1)
        {
            ActivateStep(nextIndex);
        }
        else
        {
            FinishGuide();
        }
    }

    [ProButton]
    public void ReactivateStep(string stepID)
    {
        if (currentGuideRuntime == null)
        {
            Debug.LogWarning("[GuideStepManager] No active guide!");
            return;
        }

        int index = currentGuideRuntime.steps.FindIndex(s => s.stepID == stepID);
        if (index == -1)
        {
            Debug.LogWarning($"[GuideStepManager] Step '{stepID}' not found!");
            return;
        }

        currentGuideRuntime.steps[index].isCompleted = false;
        ActivateStep(index);

        Debug.Log($"[GuideStepManager] Reactivated step: {stepID}");
    }

    [ProButton]
    public void RollbackStep()
    {
        if (currentStepIndex <= 0)
        {
            Debug.LogWarning("[GuideStepManager] Already at first step!");
            return;
        }

        currentStep.isCompleted = false;
        currentStepIndex--;
        ActivateStep(currentStepIndex);

        Debug.Log($"[GuideStepManager] Rolled back to step: {currentStep.stepID}");
    }

    private void FinishGuide()
    {
        if (titleText != null)
            titleText.text = "Hãy về phía quyển sách để xem kết quả nhé!";
        if (descriptionText != null)
            descriptionText.text = "Các bước chuẩn bị đã hoàn thành, bạn hãy di về phía quyển sách để xem kết quả được hiển thị.";

        Debug.Log("[GuideStepManager] All steps completed! Experiment started.");

        OnGuideFinished?.Invoke(currentGuideID);
    }

    private int FindLastUncompletedStepBefore(int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            if (!currentGuideRuntime.steps[i].isCompleted)
                return i;
        }
        return -1;
    }

    private int FindNextUncompletedStepAfter(int index)
    {
        for (int i = index + 1; i < currentGuideRuntime.steps.Count; i++)
        {
            if (!currentGuideRuntime.steps[i].isCompleted)
                return i;
        }
        return -1;
    }

    [ProButton]
    public void RestartGuide()
    {
        if (currentGuideAsset == null)
        {
            Debug.LogWarning("[GuideStepManager] No guide selected to restart!");
            return;
        }

        if (currentGuideRuntime != null)
            Destroy(currentGuideRuntime);

        currentGuideRuntime = Instantiate(currentGuideAsset);
        currentStep = null;
        currentStepIndex = -1;
        currentStepID = "";
        currentStatus = $"{currentGuideID} (Restarted)";

        if (currentGuideRuntime.steps.Count > 0)
            ActivateStep(0);
    }

    public void DoneExperiment()
    {
        if (titleText != null)
            titleText.text = "Hoàn thành thí nghiệm";
        if (descriptionText != null)
            descriptionText.text = "Bạn đã hoàn thành thí nghiệm, hãy tìm các NPC khác để được trải nghiệm các thí nghiệm khác";

        coroutine = StartCoroutine(RestartTitle(10f));
    }

    private IEnumerator RestartTitle(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (titleText != null)
            titleText.text = "";
        if (descriptionText != null)
            descriptionText.text = "";
    }

    public void StopCouroutine()
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
    }
}