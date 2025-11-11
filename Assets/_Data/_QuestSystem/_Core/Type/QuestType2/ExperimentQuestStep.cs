using DreamClass.QuestSystem;
using UnityEngine;

public class ExperimentQuestStep : QuestStep
{
    [Header("Guide Step Tracking")]
    [Tooltip("Chế độ tracking: Single (1 step) hoặc Multiple (nhiều steps)")]
    [SerializeField] private TrackingMode trackingMode = TrackingMode.Single;
    
    [Tooltip("Guide Step ID cần hoàn thành (dùng cho Single mode)")]
    [SerializeField] private string targetGuideStepID;
    
    [Tooltip("Danh sách Guide Step IDs (dùng cho Multiple mode)")]
    [SerializeField] private string[] targetGuideStepIDs;
    
    [Tooltip("Yêu cầu hoàn thành tất cả hay chỉ một trong số các steps")]
    [SerializeField] private CompletionRequirement completionRequirement = CompletionRequirement.All;

    public enum TrackingMode
    {
        None,
        Single,
        Multiple
    }

    public enum CompletionRequirement
    {
        All,
        Any,
        Minimum
    }
    
    [Tooltip("Số lượng tối thiểu steps cần hoàn thành (chỉ dùng với Minimum mode)")]
    [SerializeField] private int minCompletedCount = 1;
    
    [Header("Experiment Action")]
    [SerializeField] private ExperimentAction actionType;
    
    [Header("Validation")]
    [SerializeField] private bool requireExperimentRunning = false;

    private QuestType2 questType2;
    private bool isMonitoring = false;
    private bool hasSubscribed = false;
    private GameController expController;

    public enum ExperimentAction
    {
        None,
        SetupExperiment,
        StartExperiment,
        WaitForCompletion,
        StopExperiment
    }

    protected override void LoadComponents()
    {
        base.LoadComponents();
        questType2 = questCtrl as QuestType2;
        
        if (questType2 == null)
        {
            Debug.LogWarning($"[ExperimentQuestStep] QuestCtrl is not QuestType2!");
        }
        else
        {
            expController = questType2.GetExperimentController();
        }
    }

    public override void StartStep()
    {
        base.StartStep();
        isMonitoring = true;

        Debug.Log($"[ExperimentQuestStep] Started step: {StepId}");

        // Subscribe to GameController events
        SubscribeToGameControllerEvents();

        // Thực hiện action nếu có
        ExecuteAction();

        // BẮT ĐẦU TRACKING từ GameController
        StartGameControllerTracking();

        // Check ngay lập tức
        CheckCompletion();
    }

    /// <summary>
    /// Subscribe to GameController events (không bị ảnh hưởng bởi disable)
    /// </summary>
    private void SubscribeToGameControllerEvents()
    {
        if (hasSubscribed || expController == null) return;

        // Subscribe to tracking events
        expController.OnGuideStepStatusChanged += HandleGuideStepStatusChanged;
        expController.OnExperimentStateChanged += HandleExperimentStateChanged;
        expController.OnExperimentCompleted += HandleExperimentCompleted;
        
        hasSubscribed = true;
        Debug.Log($"[ExperimentQuestStep] Subscribed to GameController events");
    }

    private void UnsubscribeFromGameControllerEvents()
    {
        if (!hasSubscribed || expController == null) return;

        expController.OnGuideStepStatusChanged -= HandleGuideStepStatusChanged;
        expController.OnExperimentStateChanged -= HandleExperimentStateChanged;
        expController.OnExperimentCompleted -= HandleExperimentCompleted;
        
        hasSubscribed = false;
        Debug.Log($"[ExperimentQuestStep] Unsubscribed from GameController events");
    }

    /// <summary>
    /// Bắt đầu tracking từ GameController
    /// </summary>
    private void StartGameControllerTracking()
    {
        if (expController == null) return;

        // Tùy theo tracking mode, yêu cầu GameController track các steps cần thiết
        if (trackingMode == TrackingMode.Single && !string.IsNullOrEmpty(targetGuideStepID))
        {
            expController.StartTrackingGuideSteps(targetGuideStepID);
        }
        else if (trackingMode == TrackingMode.Multiple && targetGuideStepIDs != null && targetGuideStepIDs.Length > 0)
        {
            expController.StartTrackingGuideSteps(targetGuideStepIDs);
        }
    }

    /// <summary>
    /// Callback khi GameController phát hiện Guide Step status thay đổi
    /// </summary>
    private void HandleGuideStepStatusChanged(string stepID, bool isCompleted)
    {
        if (!isMonitoring || IsComplete) return;

        // Check nếu step này có liên quan
        bool isRelevant = false;

        if (trackingMode == TrackingMode.Single)
        {
            isRelevant = stepID == targetGuideStepID;
        }
        else if (trackingMode == TrackingMode.Multiple && targetGuideStepIDs != null)
        {
            isRelevant = System.Array.Exists(targetGuideStepIDs, id => id == stepID);
        }

        if (isRelevant)
        {
            //Debug.Log($"[ExperimentQuestStep] Guide step status changed: {stepID} = {isCompleted}");
            CheckCompletion();
        }
    }

    private void HandleExperimentStateChanged(bool isRunning)
    {
        if (!isMonitoring || IsComplete) return;

        Debug.Log($"[ExperimentQuestStep] Experiment state changed: {(isRunning ? "Running" : "Stopped")}");
        CheckCompletion();
    }

    private void HandleExperimentCompleted()
    {
        if (!isMonitoring || IsComplete) return;

        Debug.Log($"[ExperimentQuestStep] Experiment completed");
        
        if (actionType == ExperimentAction.WaitForCompletion)
        {
            CheckCompletion();
        }
    }

    private void CheckCompletion()
    {
        //Debug.Log($"[ExperimentQuestStep] Checking completion...");

        // Check Guide Step completion
        bool guideStepCompleted = CheckGuideStepCompleted();
        
        // Check Experiment state nếu cần
        bool experimentConditionMet = CheckExperimentCondition();

        // Hoàn thành nếu tất cả điều kiện đều đạt
        if (guideStepCompleted && experimentConditionMet)
        {
            CompleteStep();
        }
    }

    private bool CheckGuideStepCompleted()
    {
        if (trackingMode == TrackingMode.Single)
        {
            return CheckSingleGuideStep();
        }
        else if (trackingMode == TrackingMode.Multiple)
        {
            return CheckMultipleGuideSteps();
        }
        return true;
    }

    private bool CheckSingleGuideStep()
    {
        if (string.IsNullOrEmpty(targetGuideStepID))
            return true;

        return IsGuideStepCompleted(targetGuideStepID);
    }

    private bool CheckMultipleGuideSteps()
    {
        if (targetGuideStepIDs == null || targetGuideStepIDs.Length == 0)
            return true;

        int completedCount = 0;
        
        foreach (string stepID in targetGuideStepIDs)
        {
            if (IsGuideStepCompleted(stepID))
            {
                completedCount++;
            }
        }

        switch (completionRequirement)
        {
            case CompletionRequirement.All:
                return completedCount == targetGuideStepIDs.Length;

            case CompletionRequirement.Any:
                return completedCount > 0;

            case CompletionRequirement.Minimum:
                return completedCount >= Mathf.Min(minCompletedCount, targetGuideStepIDs.Length);

            default:
                return false;
        }
    }

    private bool IsGuideStepCompleted(string stepID)
    {
        if (string.IsNullOrEmpty(stepID))
            return false;

        var guideManager = GuideStepManager.Instance;
        if (guideManager == null || guideManager.CurrentGuideRuntime == null)
            return false;

        var step = guideManager.CurrentGuideRuntime.steps.Find(s => s.stepID == stepID);
        return step != null && step.isCompleted;
    }

    private bool CheckExperimentCondition()
    {
        if (expController == null) return true;

        switch (actionType)
        {
            case ExperimentAction.WaitForCompletion:
                return !expController.IsExperimentRunning();

            case ExperimentAction.StartExperiment:
                return expController.IsExperimentRunning();

            default:
                return !requireExperimentRunning || expController.IsExperimentRunning();
        }
    }

    private void ExecuteAction()
    {
        if (expController == null || actionType == ExperimentAction.None)
            return;

        switch (actionType)
        {
            case ExperimentAction.SetupExperiment:
                expController.SetupExperiment();
                Debug.Log($"[ExperimentQuestStep] Setup experiment");
                break;

            case ExperimentAction.StartExperiment:
                expController.StartExperiment();
                Debug.Log($"[ExperimentQuestStep] Started experiment");
                break;

            case ExperimentAction.StopExperiment:
                expController.StopExperiment();
                Debug.Log($"[ExperimentQuestStep] Stopped experiment");
                break;
        }
    }

    private void CompleteStep()
    {
        //Debug.Log($"[ExperimentQuestStep] Completed step: {StepId}");
        isMonitoring = false;
        
        // Dừng tracking từ GameController
        if (expController != null)
        {
            expController.StopAllTracking();
        }
        
        UnsubscribeFromGameControllerEvents();
        OnComplete();
    }

    /// <summary>
    /// Debug helper - hiển thị trạng thái tracking
    /// </summary>
    public string GetTrackingStatus()
    {
        if (expController != null)
        {
            return expController.GetTrackingInfo();
        }
        return "No controller";
    }

    public override void OnUpdate(object context)
    {
        // Legacy support
        if (context is string stepID)
        {
            HandleGuideStepStatusChanged(stepID, true);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameControllerEvents();
    }
}