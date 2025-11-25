using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Quản lý Material và baseMap texture cho Water Transport Experiment
/// Cơ chế can thiệp vào Mesh Render Material duy nhất -> thay đổi baseMap
/// </summary>
public class WaterTransportMaterialHandler : MonoBehaviour
{
    private List<Material> targetMaterials = new List<Material>();
    private List<Texture2D> availableTextures = new List<Texture2D>();
    
    [Header("Material Settings")]
    [SerializeField] private string baseMapPropertyName = "_BaseMap"; // Standard shader
    [SerializeField] private string alternativePropertyName = "_MainTex"; // Fallback for older shaders

    private Coroutine transitionCoroutine;

    /// <summary>
    /// Initialize handler với cloned materials và textures
    /// </summary>
    public void Initialize(List<Material> materials, List<Texture2D> textures)
    {
        targetMaterials = new List<Material>(materials);
        availableTextures = new List<Texture2D>(textures);

        Debug.Log($"[WaterTransportMaterialHandler] Initialized with {targetMaterials.Count} materials and {availableTextures.Count} textures");

        // Verify textures are valid
        foreach (var tex in availableTextures)
        {
            if (tex == null)
            {
                Debug.LogWarning("[WaterTransportMaterialHandler] One or more textures are null!");
            }
        }
    }

    /// <summary>
    /// Thay đổi baseMap cho tất cả materials
    /// </summary>
    public void ChangeBaseMap(Texture2D newTexture)
    {
        if (newTexture == null)
        {
            Debug.LogWarning("[WaterTransportMaterialHandler] Cannot set null texture!");
            return;
        }

        foreach (Material mat in targetMaterials)
        {
            if (mat == null) continue;

            // Try standard shader property first
            if (mat.HasProperty(baseMapPropertyName))
            {
                mat.SetTexture(baseMapPropertyName, newTexture);
            }
            // Fallback to older shader property
            else if (mat.HasProperty(alternativePropertyName))
            {
                mat.SetTexture(alternativePropertyName, newTexture);
            }
            else
            {
                Debug.LogWarning($"[WaterTransportMaterialHandler] Material {mat.name} has neither {baseMapPropertyName} nor {alternativePropertyName}!");
            }
        }

        //Debug.Log($"[WaterTransportMaterialHandler] Changed baseMap to texture: {newTexture.name}");
    }

    /// <summary>
    /// Transition từ texture này sang texture khác với animation
    /// Sử dụng color alpha để fade effect
    /// </summary>
    public IEnumerator TransitionBaseMap(Texture2D fromTexture, Texture2D toTexture, float duration)
    {
        if (fromTexture == null || toTexture == null)
        {
            Debug.LogWarning("[WaterTransportMaterialHandler] Cannot transition with null textures!");
            yield break;
        }

        // Set starting texture
        ChangeBaseMap(fromTexture);

        float elapsedTime = 0f;
        const float stepTime = 0.05f; // Update every 50ms

        while (elapsedTime < duration)
        {
            elapsedTime += stepTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);

            // Update alpha for fade effect
            foreach (Material mat in targetMaterials)
            {
                if (mat == null) continue;

                // Calculate color with alpha
                Color currentColor = mat.color;
                currentColor.a = Mathf.Lerp(1f, 0.5f, progress);
                mat.color = currentColor;
            }

            yield return new WaitForSeconds(stepTime);
        }

        // Set final texture
        ChangeBaseMap(toTexture);

        // Reset alpha
        foreach (Material mat in targetMaterials)
        {
            if (mat == null) continue;
            Color finalColor = mat.color;
            finalColor.a = 1f;
            mat.color = finalColor;
        }

        Debug.Log($"[WaterTransportMaterialHandler] Transitioned from {fromTexture.name} to {toTexture.name}");
    }

    /// <summary>
    /// Get current baseMap texture từ first material
    /// </summary>
    public Texture2D GetCurrentBaseMap()
    {
        if (targetMaterials.Count == 0)
        {
            Debug.LogWarning("[WaterTransportMaterialHandler] No materials assigned!");
            return null;
        }

        Material firstMaterial = targetMaterials[0];
        if (firstMaterial == null) return null;

        if (firstMaterial.HasProperty(baseMapPropertyName))
        {
            return (Texture2D)firstMaterial.GetTexture(baseMapPropertyName);
        }
        else if (firstMaterial.HasProperty(alternativePropertyName))
        {
            return (Texture2D)firstMaterial.GetTexture(alternativePropertyName);
        }

        return null;
    }

    /// <summary>
    /// Reset tất cả materials về alpha = 1
    /// </summary>
    public void ResetMaterialAlpha()
    {
        foreach (Material mat in targetMaterials)
        {
            if (mat == null) continue;
            Color color = mat.color;
            color.a = 1f;
            mat.color = color;
        }
    }

    /// <summary>
    /// Lấy số lượng materials đang quản lý
    /// </summary>
    public int GetMaterialCount()
    {
        return targetMaterials.Count;
    }

    /// <summary>
    /// Lấy số lượng textures available
    /// </summary>
    public int GetTextureCount()
    {
        return availableTextures.Count;
    }
}
