using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Quản lý việc mô phỏng quá trình vận chuyển nước trong thân cây
/// </summary>
public class WaterTransportSimulator : MonoBehaviour
{
    private List<Texture2D> transportStages = new List<Texture2D>();
    
    [Header("Transport Simulation")]
    [SerializeField] private float transportSpeed = 1f; // Tốc độ vận chuyển
    [SerializeField] private int currentStage = 0;

    private bool isTransporting = false;

    /// <summary>
    /// Initialize simulator với list textures đại diện cho các giai đoạn vận chuyển
    /// </summary>
    public void Initialize(List<Texture2D> textures)
    {
        transportStages = new List<Texture2D>(textures);
        currentStage = 0;
        isTransporting = false;

        Debug.Log($"[WaterTransportSimulator] Initialized with {transportStages.Count} stages");
    }

    /// <summary>
    /// Lấy texture tại stage hiện tại
    /// </summary>
    public Texture2D GetCurrentStageTexture()
    {
        if (transportStages.Count == 0)
        {
            Debug.LogWarning("[WaterTransportSimulator] No stages available!");
            return null;
        }

        return transportStages[currentStage % transportStages.Count];
    }

    /// <summary>
    /// Lấy texture tại stage tiếp theo
    /// </summary>
    public Texture2D GetNextStageTexture()
    {
        if (transportStages.Count == 0)
        {
            Debug.LogWarning("[WaterTransportSimulator] No stages available!");
            return null;
        }

        int nextStage = (currentStage + 1) % transportStages.Count;
        return transportStages[nextStage];
    }

    /// <summary>
    /// Chuyển đến stage tiếp theo
    /// </summary>
    public void AdvanceToNextStage()
    {
        if (transportStages.Count == 0) return;

        currentStage = (currentStage + 1) % transportStages.Count;
        Debug.Log($"[WaterTransportSimulator] Advanced to stage {currentStage}");
    }

    /// <summary>
    /// Chuyển đến stage cụ thể
    /// </summary>
    public void GoToStage(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= transportStages.Count)
        {
            Debug.LogWarning($"[WaterTransportSimulator] Invalid stage index: {stageIndex}");
            return;
        }

        currentStage = stageIndex;
        Debug.Log($"[WaterTransportSimulator] Changed to stage {stageIndex}");
    }

    /// <summary>
    /// Lấy số lượng stages
    /// </summary>
    public int GetStageCount()
    {
        return transportStages.Count;
    }

    /// <summary>
    /// Lấy stage hiện tại
    /// </summary>
    public int GetCurrentStage()
    {
        return currentStage;
    }

    /// <summary>
    /// Set trạng thái transporting
    /// </summary>
    public void SetTransporting(bool transporting)
    {
        isTransporting = transporting;
    }

    /// <summary>
    /// Check if currently transporting
    /// </summary>
    public bool IsTransporting()
    {
        return isTransporting;
    }

    /// <summary>
    /// Reset simulator về trạng thái ban đầu
    /// </summary>
    public void Reset()
    {
        currentStage = 0;
        isTransporting = false;
        Debug.Log("[WaterTransportSimulator] Simulator reset!");
    }
}
