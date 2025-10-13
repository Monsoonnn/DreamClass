using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

public class ColorIndicator : MonoBehaviour {
    [Header("References")]
    public GameObject colorIndicatorModel;

    [Header("Material To Apply")]
    public Material colorMaterial;

   [ProButton]
    public void ApplyMaterial() {
        if (colorIndicatorModel != null && colorMaterial != null) {
            var renderer = colorIndicatorModel.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material = colorMaterial;
                Debug.Log("Material applied at runtime!");
            } else {
                Debug.LogWarning("Renderer not found on colorIndicatorModel.");
            }
        } else {
            Debug.LogWarning("colorIndicatorModel or colorMaterial is not set.");
        }
    }
}
