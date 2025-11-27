using System.Collections.Generic;

/// <summary>
/// Kết quả tóm tắt cho Exam
/// </summary>
[System.Serializable]
public class ExamStepSummary
{
    public int totalSteps;
    public int completedSteps;
    public int totalErrors;
    public int totalRollbacks;
    public float totalTimeSeconds;
    public List<string> errorSteps = new List<string>();

    public float GetCompletionPercentage()
    {
        return totalSteps > 0 ? (float)completedSteps / totalSteps * 100f : 0f;
    }
}
