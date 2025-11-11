using UnityEngine;
using TMPro;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using System.ComponentModel;
using System.Linq;
using System;
using System.Collections;

/// <summary>
/// Controls the logic flow of guide steps across multiple guides.
/// Uses runtime copies of GuideData to avoid modifying the original assets.
/// </summary>
public class GuideStepManager : SingletonCtrl<GuideStepManager> {
    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [SerializeField] List<GameController> gameControllerList = new List<GameController>();

    public List<GameController> GameControllerList => gameControllerList;

    [Header("Active Game")]
    [SerializeField] public GameController gameController;

    [Header("Guides (Asset References)")]
    [Tooltip("All available guide ScriptableObjects")]
    public List<GuideData> allGuides = new List<GuideData>();

    [Header("Runtime Info (Debug Only)")]
    public string currentGuideID;
    public string currentStepID;
    public string currentStatus;

    // Internal runtime data
    private GuideData currentGuideAsset;          // Reference to original SO
    private GuideData currentGuideRuntime;
    public GuideData CurrentGuideRuntime => currentGuideRuntime;        // Instantiated runtime copy
    private StepData currentStep;                 // Current active step instance
    private int currentStepIndex = -1;

    private Coroutine coroutine = null;

    // ===== EVENTS =====
    public event Action<string> OnStepCompleted;
    public event Action<string> OnStepActivated;
    public event Action<string> OnGuideStarted;
    public event Action<string> OnGuideFinished;

    // <summary>
    /// UPDATE method CompleteStep() hiện tại
    /// Thêm event trigger
    /// </summary>
    public void CompleteStep_Updated(string stepID)
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

        // ===== TRIGGER EVENT =====
        OnStepCompleted?.Invoke(stepID);
        // =========================

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

    /// <summary>
    /// UPDATE method ActivateStep() hiện tại
    /// Thêm event trigger
    /// </summary>
    public void ActivateStep_Updated(int index)
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

        // Ensure previous steps are completed
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

        // Update UI
        if (titleText != null) titleText.text = step.title;
        if (descriptionText != null) descriptionText.text = step.description;

        // Visual hint
        if (step.highlightTarget != null)
            Debug.Log($"Highlighting target: {step.highlightTarget.name}");

        currentStatus = $"{currentGuideID} / Step {index + 1}: {step.stepID}";
        Debug.Log($"[GuideStepManager] Activated step: {step.stepID}");

        // ===== TRIGGER EVENT =====
        OnStepActivated?.Invoke(step.stepID);
        // =========================
    }

    /// <summary>
    /// UPDATE method SetCurrentGuide() hiện tại
    /// Thêm event trigger
    /// </summary>
    public void SetCurrentGuide_Updated(string guideID)
    {
        GuideData found = allGuides.Find(g => g.guideID == guideID);
        if (found == null)
        {
            Debug.LogWarning($"[GuideStepManager] Guide '{guideID}' not found!");
            return;
        }

        // Create a runtime copy
        currentGuideRuntime = Instantiate(found);
        currentGuideAsset = found;
        currentGuideID = guideID;

        Debug.Log($"[GuideStepManager] Switched to guide: {guideID}");

        // ===== TRIGGER EVENT =====
        OnGuideStarted?.Invoke(guideID);
        // =========================

        // Start at first step if available
        if (currentGuideRuntime.steps.Count > 0)
            ActivateStep(0);
    }

    /// <summary>
    /// UPDATE method FinishGuide() hiện tại
    /// Thêm event trigger
    /// </summary>
    private void FinishGuide_Updated()
    {
        if (titleText != null)
            titleText.text = "Hãy về phía quyển sách để xem kết quả nhé!";
        if (descriptionText != null)
            descriptionText.text = "Các bước chuẩn bị đã hoàn thành, bạn hãy di về phía quyển sách để xem kết quả được hiển thị.";

        if (gameController is Experiment exp)
        {
            exp.StartExperiment();
        }

        Debug.Log("[GuideStepManager] All steps completed! Experiment started.");

        // ===== TRIGGER EVENT =====
        OnGuideFinished?.Invoke(currentGuideID);
        // =========================
    }


    protected override void LoadComponents()
    {
        base.LoadComponents();
        this.LoadAllGameplay();
    }
    
    [ProButton]
    protected virtual void LoadAllGameplay() {
        // If list is null, empty, or contains any null/missing elements
        if (gameControllerList != null || gameControllerList.Count > 0 || gameControllerList.Any(g => g != null)) return;
        gameControllerList = FindObjectsByType<GameController>(FindObjectsSortMode.None).ToList();
    }

    protected override void Start() {
        // // Auto-load first guide for testing
        // if (allGuides.Count > 0) {
        //     SetCurrentGuide(allGuides[0].guideID);
        // }

      /*  if(gameController == null) gameController = GameObject.FindAnyObjectByType<GameController>();*/

    }

    [ProButton]
    protected virtual void LoadGameplay( string id ) {
        GameController tempGc = null;
        foreach (GameController gc in gameControllerList) { 
            if(gc.GetExperimentName() == id) tempGc = gc;
        }
        if (tempGc != null) {
            if (gameController != null) {
                gameController.transform.parent.gameObject.SetActive(false);
                gameController.OnDisableGame();
            }
            tempGc.transform.parent.gameObject.SetActive(true);
            tempGc.OnActiveGame();
        } 
        else Debug.LogWarning("[GuideStepManager] No game found with ID: " + id);
    }

    /// <summary>
    /// Switches to a new guide by ID, creating a runtime copy to prevent asset modification.
    /// </summary>
    [ProButton]
    public void SetCurrentGuide( string guideID ) {
        GuideData found = allGuides.Find(g => g.guideID == guideID);
        if (found == null) {
            Debug.LogWarning($"[GuideStepManager] Guide '{guideID}' not found!");
            return;
        }

        // Create a runtime copy
        currentGuideRuntime = Instantiate(found);
        currentGuideAsset = found;
        currentGuideID = guideID;

        Debug.Log($"[GuideStepManager] Switched to guide: {guideID}");

        // Start at first step if available
        if (currentGuideRuntime.steps.Count > 0)
            ActivateStep(0);
    }

    /// <summary>
    /// Activates a step by index (within runtime copy).
    /// </summary>
    public void ActivateStep( int index ) {
        if (currentGuideRuntime == null || currentGuideRuntime.steps.Count == 0) {
            Debug.LogWarning("[GuideStepManager] No active guide!");
            return;
        }

        if (index < 0 || index >= currentGuideRuntime.steps.Count) {
            Debug.LogWarning("[GuideStepManager] Invalid step index!");
            return;
        }

        // Ensure previous steps are completed
        int rollbackIndex = FindLastUncompletedStepBefore(index);
        if (rollbackIndex != -1 && rollbackIndex < index) {
            Debug.LogWarning($"[GuideStepManager] Cannot activate step {index}, previous step {rollbackIndex} not completed! Rolling back...");
            ActivateStep(rollbackIndex);
            return;
        }

        StepData step = currentGuideRuntime.steps[index];
        currentStep = step;
        currentStepIndex = index;
        currentStepID = step.stepID;
        step.isCompleted = false;

        // Update UI
        if (titleText != null) titleText.text = step.title;
        if (descriptionText != null) descriptionText.text = step.description;

        // Visual hint
        if (step.highlightTarget != null)
            Debug.Log($"Highlighting target: {step.highlightTarget.name}");

        currentStatus = $"{currentGuideID} / Step {index + 1}: {step.stepID}";
        Debug.Log($"[GuideStepManager] Activated step: {step.stepID}");
    }

    /// <summary>
    /// Activates a step by its stepID.
    /// </summary>
    [ProButton]
    public void ActivateStep( string stepID ) {
        if (currentGuideRuntime == null || currentGuideRuntime.steps.Count == 0) {
            Debug.LogWarning("[GuideStepManager] No active guide!");
            return;
        }

        int index = currentGuideRuntime.steps.FindIndex(s => s.stepID == stepID);
        if (index == -1) {
            Debug.LogWarning($"[GuideStepManager] Step '{stepID}' not found in guide '{currentGuideID}'!");
            return;
        }

        ActivateStep(index);
    }

    /// <summary>
    /// Completes the current step and moves to the next uncompleted one.
    /// </summary>
    [ProButton]
    public void CompleteStep( string stepID ) {
        if (currentStep == null ) {
            Debug.LogWarning("[GuideStepManager] No active step!");
            return;
        }

        if (currentStep.stepID != stepID ) {
            Debug.LogWarning($"[GuideStepManager] Tried to complete '{stepID}', but current step is '{currentStep.stepID}'!");
            return;
        }

        currentStep.isCompleted = true;
        Debug.Log($"[GuideStepManager] Step completed: {currentStep.stepID}");

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
    public void ReactivateStep( string stepID ) {
        if (currentGuideRuntime == null) {
            Debug.LogWarning("[GuideStepManager] No active guide!");
            return;
        }

        int index = currentGuideRuntime.steps.FindIndex(s => s.stepID == stepID);
        if (index == -1) {
            Debug.LogWarning($"[GuideStepManager] Step '{stepID}' not found!");
            return;
        }

        // Mark step incomplete
        currentGuideRuntime.steps[index].isCompleted = false;

        // Activate this step
        ActivateStep(index);

        Debug.Log($"[GuideStepManager] Reactivated step: {stepID}");
    }


    /// <summary>
    /// Roll back one step (if possible).
    /// </summary>
    [ProButton]
    public void RollbackStep() {
        if (currentStepIndex <= 0) {
            Debug.LogWarning("[GuideStepManager] Already at first step!");
            return;
        }

        currentStep.isCompleted = false;
        currentStepIndex--;
        ActivateStep(currentStepIndex);

        Debug.Log($"[GuideStepManager] Rolled back to step: {currentStep.stepID}");
    }

    /// <summary>
    /// Finishes the current guide and triggers the experiment.
    /// </summary>
    private void FinishGuide() {
        if (titleText != null)
            titleText.text = "Hãy về phía quyển sách để xem kết quả nhé!";
        if (descriptionText != null)
            descriptionText.text = "Các bước chuẩn bị đã hoàn thành, bạn hãy di về phía quyển sách để xem kết quả được hiển thị.";

        // if (gameController is Experiment exp) {
        //     exp.StartExperiment();
        // }


        Debug.Log("[GuideStepManager] All steps completed! Experiment started.");
    }

    /// <summary>
    /// Finds the last uncompleted step before a given index.
    /// Returns -1 if all previous are done.
    /// </summary>
    private int FindLastUncompletedStepBefore( int index ) {
        for (int i = index - 1; i >= 0; i--) {
            if (!currentGuideRuntime.steps[i].isCompleted)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Finds the next uncompleted step after a given index.
    /// Returns -1 if all remaining are done.
    /// </summary>
    private int FindNextUncompletedStepAfter( int index ) {
        for (int i = index + 1; i < currentGuideRuntime.steps.Count; i++) {
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

        // Destroy old runtime copy (just to be clean)
        if (currentGuideRuntime != null)
            Destroy(currentGuideRuntime);
        if (gameController != null) gameController.StopExperiment();

        // Re-instantiate a fresh runtime copy
        currentGuideRuntime = Instantiate(currentGuideAsset);
        currentStep = null;
        currentStepIndex = -1;
        currentStepID = "";
        currentStatus = $"{currentGuideID} (Restarted)";

        Debug.Log($"[GuideStepManager] Restarted guide: {currentGuideID}");

        // Start again from first step
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
