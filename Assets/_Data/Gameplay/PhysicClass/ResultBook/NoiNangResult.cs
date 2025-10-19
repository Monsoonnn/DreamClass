using com.cyborgAssets.inspectorButtonPro;
using TMPro;
using UnityEngine;

public class NoiNangRes : NewMonobehavior {
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField inputTempBefore;
    [SerializeField] private TMP_InputField inputTempAfter;

    [Header("Result Texts")]
    [SerializeField] private TextMeshProUGUI noiNangBefore;
    [SerializeField] private TextMeshProUGUI noiNangAfter;

    [SerializeField] private Experiment2 experiment;

    protected override void Start() {
        ResetUI();
        inputTempBefore.onEndEdit.AddListener(OnBeforeEntered);
        inputTempAfter.onEndEdit.AddListener(OnAfterEntered);
    }

    private void OnBeforeEntered( string value ) {
        if (string.IsNullOrWhiteSpace(value)) return;

        experiment.guideStepManager.CompleteStep("TEMP_BEFORE");

        // Show result
        noiNangBefore.text = value;
        noiNangBefore.gameObject.SetActive(true);
        noiNangAfter.gameObject.SetActive(true);

        // Switch to next input
        inputTempBefore.gameObject.SetActive(false);
        inputTempAfter.gameObject.SetActive(true);
    }

    private void OnAfterEntered( string value ) {
        if (string.IsNullOrWhiteSpace(value)) return;


        experiment.guideStepManager.CompleteStep("TEMP_AFTER");

        // Show result
        noiNangAfter.text = value;
        noiNangAfter.gameObject.SetActive(true);
        noiNangBefore.gameObject.SetActive(true);

        // Hide inputs
        inputTempBefore.gameObject.SetActive(false);
        inputTempAfter.gameObject.SetActive(false);

        experiment.StopExperiment();
    }

    [ProButton]
    public void Restart() {
        ResetUI();
    }

    private void ResetUI() {
        // Clear all text
        inputTempBefore.text = string.Empty;
        inputTempAfter.text = string.Empty;

        noiNangBefore.gameObject.SetActive(false);
        noiNangAfter.gameObject.SetActive(false);

        // Only activate before input first
        inputTempBefore.gameObject.SetActive(true);
        inputTempAfter.gameObject.SetActive(false);
    }

    [ProButton]
    public virtual void SimulateInputBefore( float value = 22.3f ) {
        inputTempBefore.text = value.ToString("F1") + "°C";
        OnBeforeEntered(inputTempBefore.text);
    }

    [ProButton]
    public virtual void SimulateInputAfter( float value = 25.5f ) {
        inputTempAfter.text = value.ToString("F1") + "°C";
        OnAfterEntered(inputTempAfter.text);
    }
}
