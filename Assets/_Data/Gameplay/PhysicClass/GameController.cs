using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class GameController : NewMonobehavior {

    [Header("Experiment State Tracking")]
    protected bool isExperimentRunning = false;

    public GuideStepManager guideStepManager;

    // ===== EVENTS =====
    public event Action<bool> OnExperimentStateChanged; 
    public event Action<bool> OnProgressTracking;
    public event Action OnExperimentCompleted;
    
    // NEW: Event cho Guide Step tracking
    public event Action<string, bool> OnGuideStepStatusChanged;

    // NEW: Tracking configuration
    private bool isTrackingActive = false;
    private HashSet<string> trackedStepIDs = new HashSet<string>();
    private Dictionary<string, bool> lastStepStatus = new Dictionary<string, bool>(); // Cache status
    private Coroutine trackingCoroutine;
    
    [Header("Tracking Settings")]
     private float trackingInterval = 0.5f; 

    private Coroutine progressCoroutine;

    public void OnActiveGame() {
        guideStepManager.SetCurrentGuide(this.GetExperimentName());
        guideStepManager.gameController = this;
    }

    public void OnDisableGame()
    {
        guideStepManager.gameController = null;
        guideStepManager.RestartGuide();
        StopAllTracking();
    }

    public virtual void SetupExperiment()
    {
        this.transform.parent.gameObject.SetActive(true);
        Debug.Log($"[GameController] SetupExperiment not implemented for {GetExperimentName()}");
    }

    public virtual void StartExperiment() {
        isExperimentRunning = true;
        OnExperimentStateChanged?.Invoke(true);
        guideStepManager.StopCouroutine();
        Debug.Log($"[GameController] Started experiment: {GetExperimentName()}");
    }

    public virtual void StopExperiment() {
        isExperimentRunning = false;
        guideStepManager.DoneExperiment();
        this.transform.parent.gameObject.SetActive(false);
        Debug.Log($"[GameController] Stopped experiment: {GetExperimentName()}");
        OnExperimentStateChanged?.Invoke(false);
    }

    public abstract string GetExperimentName();

    public void ActiveGamePlay(string id)
    {
        if (id == GetExperimentName()) transform.parent.gameObject.SetActive(true);
        return;
    }

    public virtual bool IsExperimentRunning()
    {
        return isExperimentRunning;
    }

    protected void NotifyExperimentCompleted()
    {
        OnExperimentCompleted?.Invoke();
    }

    // ===== NEW TRACKING METHODS =====
    
    /// <summary>
    /// Bắt đầu tracking một hoặc nhiều Guide Steps
    /// </summary>
    public void StartTrackingGuideSteps(params string[] stepIDs)
    {
        if (stepIDs == null || stepIDs.Length == 0) return;
        
        foreach (string stepID in stepIDs)
        {
            if (!string.IsNullOrEmpty(stepID))
            {
                trackedStepIDs.Add(stepID);
            }
        }
        
        if (!isTrackingActive)
        {
            isTrackingActive = true;
            trackingCoroutine = StartCoroutine(TrackingLoop());
            Debug.Log($"[GameController] Started tracking {trackedStepIDs.Count} guide steps");
        }
        
        OnProgressTracking?.Invoke(true);
    }
    
    /// <summary>
    /// Dừng tracking một step cụ thể
    /// </summary>
    public void StopTrackingGuideStep(string stepID)
    {
        trackedStepIDs.Remove(stepID);
        
        if (trackedStepIDs.Count == 0)
        {
            StopAllTracking();
        }
    }
    
    /// <summary>
    /// Dừng toàn bộ tracking
    /// </summary>
    public void StopAllTracking()
    {
        isTrackingActive = false;
        trackedStepIDs.Clear();
        
        if (trackingCoroutine != null)
        {
            StopCoroutine(trackingCoroutine);
            trackingCoroutine = null;
        }
        
        OnProgressTracking?.Invoke(false);
        Debug.Log($"[GameController] Stopped all tracking");
    }
    
    /// <summary>
    /// Coroutine liên tục check status của các Guide Steps
    /// </summary>
    private IEnumerator TrackingLoop()
    {
        var waitInterval = new WaitForSeconds(trackingInterval);
        
        while (isTrackingActive)
        {
            if (guideStepManager != null && guideStepManager.CurrentGuideRuntime != null)
            {
                // Tạo copy của HashSet để tránh modification exception
                string[] stepIDsSnapshot = new string[trackedStepIDs.Count];
                trackedStepIDs.CopyTo(stepIDsSnapshot);
                
                foreach (string stepID in stepIDsSnapshot)
                {
                    // Check xem step còn trong tracking set không (có thể đã bị remove)
                    if (!trackedStepIDs.Contains(stepID)) continue;
                    
                    bool isCompleted = CheckGuideStepStatus(stepID);
                    
                    // Notify về status change
                    OnGuideStepStatusChanged?.Invoke(stepID, isCompleted);
                }
            }
            
            yield return waitInterval;
        }
    }
    
    /// <summary>
    /// Check status của một Guide Step
    /// </summary>
    private bool CheckGuideStepStatus(string stepID)
    {
        if (string.IsNullOrEmpty(stepID)) return false;
        if (guideStepManager == null || guideStepManager.CurrentGuideRuntime == null) return false;
        
        var step = guideStepManager.CurrentGuideRuntime.steps.Find(s => s.stepID == stepID);
        return step != null && step.isCompleted;
    }
    
    /// <summary>
    /// Get thông tin tracking status (for debug)
    /// </summary>
    public string GetTrackingInfo()
    {
        if (!isTrackingActive) return "Tracking: Inactive";
        
        int completedCount = 0;
        foreach (string stepID in trackedStepIDs)
        {
            if (CheckGuideStepStatus(stepID)) completedCount++;
        }
        
        return $"Tracking: {completedCount}/{trackedStepIDs.Count} steps completed";
    }

    // Legacy methods - giữ nguyên để tương thích
    public void StartTrackingStatus()
    {
        OnProgressTracking?.Invoke(true);
    }
    
    public void StopTrackingStatus()
    {
        OnProgressTracking?.Invoke(false);
    }
    
    private void OnDestroy()
    {
        StopAllTracking();
    }
}