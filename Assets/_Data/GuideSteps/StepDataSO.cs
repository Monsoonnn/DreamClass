using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Serializable data for a single step
/// </summary>
[System.Serializable]
public class StepData {
    [Header("Basic Info")]
    public string stepID;                        // Unique ID
    public string title;                         // Step title
    [TextArea(2, 5)] public string description; // Step description

    [Header("Visual")]
    public GameObject highlightTarget;           // Optional target to highlight

    [Header("Dependencies")]
    public string previousStepID;                // Must complete this step first

    public bool isCompleted = false;  // Runtime flag
}
