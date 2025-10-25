using TMPro;
using UnityEngine;

public class Multimeter : NewMonobehavior {
    [Header("Multimeter")]
    public TextMeshProUGUI valueUI;
    public GameObject turnOnBtn;
    public Experiment experiment;

    public bool isOn = false;

    /// <summary>
    /// Turn on/off the multimeter display.
    /// </summary>
    public void TogglePower(bool value) {
        isOn = !isOn;
        turnOnBtn.SetActive(!isOn);
        if (isOn) {
            GuideStepManager.Instance.CompleteStep("TURNON_OATKE");
        } else GuideStepManager.Instance.ActivateStep("TURNOFF_OATKE");

        if (valueUI != null)
            valueUI.text = "--";   
    }

    public void ResetStep() {
        isOn = false;
        turnOnBtn.SetActive(!isOn);
    }

    /// <summary>
    /// Display value received from GameController.
    /// </summary>
    public void UpdateDisplay( float value ) {
        if (!isOn || valueUI == null) return;
        valueUI.text = value.ToString("F2") + " W";
    }

    /// <summary>
    /// Reset display text.
    /// </summary>
    public void ResetDisplay() {
        if (valueUI != null)
            valueUI.text = "--";
    }
}
