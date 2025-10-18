using UnityEngine;
using TMPro;

public class DongHo : NewMonobehavior {
    [Header("Reference")]
    public GameObject turnOnBtn;
    public TextMeshProUGUI valueText;

    [Header("Runtime State")]
    public bool isOn = false;

    private float currentTemperature;

    protected override void Start() {
        Restart();
    }

    public void TogglePower() {
        isOn = !isOn;

        if (isOn)
            TurnOn();
        else
            TurnOff();
    }

    private void TurnOn() {
        // Hide the button
        if (turnOnBtn != null)
            turnOnBtn.SetActive(false);

        // Generate random temperature between 22.00 and 25.00
        currentTemperature = Random.Range(22f, 25f);

        // Display value with 2 decimal places
        if (valueText != null)
            valueText.text = currentTemperature.ToString("F2");
    }

    private void TurnOff() {
        // Show the button
        if (turnOnBtn != null)
            turnOnBtn.SetActive(true);

        // Display default placeholder
        if (valueText != null)
            valueText.text = "----";
    }

    public void Restart() {
        isOn = false;
        TurnOff();
    }
}
