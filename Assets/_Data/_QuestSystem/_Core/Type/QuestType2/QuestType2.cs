using DreamClass.QuestSystem;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Quest Type 2: Kiểm soát Experiment và Guide Steps
/// Tích hợp với GuideStepManager và GameController
/// </summary>
namespace DreamClass.QuestSystem
{
    public class QuestType2 : QuestCtrl
{
    [Header("Experiment Control")]
    [SerializeField] private string experimentID; // Ví dụ: "NHIET_DUNG_NUOC"
    [SerializeField] private GameController experimentController;
    
    [Header("Guide Integration")]
    [SerializeField] private string guideID; // ID của Guide tương ứng
    [SerializeField] private bool autoLoadGuide = true;

    protected override void LoadComponents()
    {
        base.LoadComponents();
        LoadExperimentController();
    }

    protected virtual void LoadExperimentController()
    {
        if (experimentController != null) return;
        
        // Tìm Experiment controller theo ID
        GameController[] controllers = GuideStepManager.Instance.GameControllerList.ToArray();
        foreach (var ctrl in controllers)
        {
            if (ctrl.GetExperimentName() == experimentID)
            {
                experimentController = ctrl;
                Debug.Log($"[QuestType2] Found experiment controller: {experimentID}");
                break;
            }
        }

        if (experimentController == null)
        {
            Debug.LogWarning($"[QuestType2] Experiment controller '{experimentID}' not found!");
        }
    }

    public override void StartQuest()
    {
        base.StartQuest();
        // Load Guide nếu cần
        if (autoLoadGuide && !string.IsNullOrEmpty(guideID))
        {
            GuideStepManager.Instance?.SetCurrentGuide(guideID);
            Debug.Log($"[QuestType2] Loaded guide: {guideID}");
        }

        // Setup experiment
        if (experimentController != null)
        {
            //experimentController.SetupExperiment();
            Debug.Log($"[QuestType2] Setup experiment: {experimentID}");
        }

        base.StartQuest();
    }

    protected override async Task OnBeforeReward()
    {
        // Dừng experiment trước khi trao thưởng
        if (experimentController != null 
            //&& experimentController.IsExperimentRunning()
        )
        {
            experimentController.StopExperiment();
            Debug.Log($"[QuestType2] Stopped experiment before reward");
        }

        await Task.CompletedTask;
    }

    protected override async Task OnAfterReward()
    {
        // Restart guide nếu muốn
        if (GuideStepManager.Instance != null)
        {
            // GuideStepManager.Instance.RestartGuide();
        }

        await base.OnAfterReward();
    }

    // Utility methods để các Step có thể gọi
    public void CompleteGuideStep(string stepID)
    {
        GuideStepManager.Instance?.CompleteStep(stepID);
    }

    public void ActivateGuideStep(string stepID)
    {
        GuideStepManager.Instance?.ActivateStep(stepID);
    }

    public GameController GetExperimentController()
    {
        return experimentController;
    }
}
}