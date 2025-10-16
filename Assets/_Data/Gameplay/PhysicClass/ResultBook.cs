using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultBook : NewMonobehavior {
    [Header("UI References")]
    public GameObject content; // Parent object that holds all result rows
    public GameObject item;    // Prefab for a single result row (contains 3 TextMeshPro fields)

    /// <summary>
    /// Add a new result row (time, temperature, power) to the table.
    /// </summary>
    public void AddResult( float time, float temperature, float power ) {
        if (content == null || item == null) {
            Debug.LogWarning("ResultBook: Missing content or item reference!");
            return;
        }

        // Instantiate a new row under the content container
        GameObject newItem = Instantiate(item, content.transform);

        // Find all TextMeshPro fields inside the row
        TextMeshProUGUI[] texts = newItem.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length < 3) {
            Debug.LogWarning("Result item is missing TextMeshPro fields!");
            return;
        }

        // Assign formatted values to each text field
        texts[0].text = $"{time:F1}s";         // Time
        texts[1].text = $"{temperature:F2}°C"; // Temperature
        texts[2].text = $"{power:F2}W";        // Power

        newItem.SetActive(true);
        
    }

    /// <summary>
    /// Remove all rows from the result table.
    /// </summary>
    public void ClearResults() {
        int childCount = content.transform.childCount;

        // Skip the first child (index 0)
        for (int i = childCount - 1; i >= 1; i--) {
            Destroy(content.transform.GetChild(i).gameObject);
        }
    }
}
