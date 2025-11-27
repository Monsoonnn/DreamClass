using System;
using System.Collections.Generic;
using UnityEngine;

public class ExamStepTracker : NewMonobehavior
{
    [Header("Exam Mode")]
    [SerializeField] private bool isExamMode = false;
    [SerializeField] private int examErrorCount = 0;
    [SerializeField] private int examRollbackCount = 0;
    [SerializeField] private List<string> examErrorSteps = new List<string>();

    public bool IsExamMode => isExamMode;
    public int ExamErrorCount => examErrorCount;
    public int ExamRollbackCount => examRollbackCount;
    public List<string> ExamErrorSteps => new List<string>(examErrorSteps);

    // Exam Events
    public event Action<string, int> OnExamError; // (stepId, totalErrors)
    public event Action<string> OnExamRollback; // (fromStepId)
    public event Action<string> OnExamStepCompleted; // (stepId)
    public event Action<string, float> OnExamStepStarted; // (stepId, startTime)

    // Tracking thời gian cho từng step trong exam
    private Dictionary<string, float> stepStartTimes = new Dictionary<string, float>();
    private Dictionary<string, float> stepCompleteTimes = new Dictionary<string, float>();

    public void EnableExamMode()
    {
        isExamMode = true;
        examErrorCount = 0;
        examRollbackCount = 0;
        examErrorSteps.Clear();
        stepStartTimes.Clear();
        stepCompleteTimes.Clear();
        Debug.Log("[ExamStepTracker] === EXAM MODE ENABLED ===");
    }

    public void DisableExamMode()
    {
        isExamMode = false;
        Debug.Log($"[ExamStepTracker] === EXAM MODE DISABLED === Errors: {examErrorCount}, Rollbacks: {examRollbackCount}");
    }

    public void RecordExamError(string stepId, string errorReason = "")
    {
        if (!isExamMode) return;
        examErrorCount++;
        examErrorSteps.Add($"{stepId}:{errorReason}");
        OnExamError?.Invoke(stepId, examErrorCount);
        Debug.Log($"[ExamStepTracker] EXAM ERROR recorded at step '{stepId}': {errorReason} (Total: {examErrorCount})");
    }

    public float GetStepCompletionTime(string stepId)
    {
        if (stepStartTimes.ContainsKey(stepId) && stepCompleteTimes.ContainsKey(stepId))
        {
            return stepCompleteTimes[stepId] - stepStartTimes[stepId];
        }
        return 0f;
    }

    public ExamStepSummary GetExamSummary(List<StepData> steps)
    {
        var summary = new ExamStepSummary
        {
            totalSteps = steps?.Count ?? 0,
            completedSteps = steps?.FindAll(s => s.isCompleted).Count ?? 0,
            totalErrors = examErrorCount,
            totalRollbacks = examRollbackCount,
            errorSteps = new List<string>(examErrorSteps)
        };
        foreach (var kvp in stepCompleteTimes)
        {
            if (stepStartTimes.ContainsKey(kvp.Key))
            {
                summary.totalTimeSeconds += kvp.Value - stepStartTimes[kvp.Key];
            }
        }
        return summary;
    }

    public void ResetExamTracking()
    {
        examErrorCount = 0;
        examRollbackCount = 0;
        examErrorSteps.Clear();
        stepStartTimes.Clear();
        stepCompleteTimes.Clear();
    }

    // Các hàm sau sẽ được gọi từ GuideStepManager
    public void OnStepStarted(string stepId, float time)
    {
        if (isExamMode)
        {
            stepStartTimes[stepId] = time;
            OnExamStepStarted?.Invoke(stepId, time);
        }
    }
    public void OnStepCompleted(string stepId, float time)
    {
        if (isExamMode)
        {
            stepCompleteTimes[stepId] = time;
            OnExamStepCompleted?.Invoke(stepId);
            Debug.Log($"[ExamStepTracker] EXAM: Step '{stepId}' completed in {GetStepCompletionTime(stepId):F2}s");
        }
    }
    public void OnRollback(string fromStepId)
    {
        if (isExamMode)
        {
            examRollbackCount++;
            OnExamRollback?.Invoke(fromStepId);
        }
    }
}