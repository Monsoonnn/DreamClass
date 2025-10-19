using UnityEngine;
using TMPro;
using com.cyborgAssets.inspectorButtonPro;

public class DongHo : NewMonobehavior {
    [Header("Reference")]
    public GameObject turnOnBtn;
    public TextMeshProUGUI valueText;
    public Experiment2 experiment;

    [Header("Runtime State")]
    public bool isOn = false;

    private float currentTemperature;

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadExperiment();
    }

    private void LoadExperiment() {
        if (experiment != null) return;
        experiment = transform.parent.GetComponent<Experiment2>();
    }

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
    [ProButton]
    private void TurnOn() {
        // Hide the button
        if (turnOnBtn != null)
            turnOnBtn.SetActive(false);

        // Display value with 2 decimal places
        if (valueText != null)
            valueText.text = currentTemperature.ToString("F2");
        experiment.StartExperiment();

        experiment.guideStepManager.CompleteStep("TURNON_NHIETKE");

    }
    [ProButton]
    private void TurnOff() {
        // Show the button
        if (turnOnBtn != null)
            turnOnBtn.SetActive(true);

        // Display default placeholder
        if (valueText != null)
            valueText.text = "----";
        experiment.StopExperiment();
        experiment.guideStepManager.ReactivateStep("TURNON_NHIETKE");
    }

    public void Restart() {
        isOn = false;
        TurnOff();
    }
}
