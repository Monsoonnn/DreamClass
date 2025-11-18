using UnityEngine;
using TMPro;
using com.cyborgAssets.inspectorButtonPro;
using System.Collections;

public class DongHo : NewMonobehavior {
    [Header("Reference")]
    public GameObject turnOnBtn;
    public TextMeshProUGUI valueText;
    public Experiment2 experiment;

    [Header("Runtime State")]
    public bool isOn = false;
    
    [Header("Update Speed")]
    [SerializeField] private float updateInterval = 2f; // Thời gian chập chờn giữa các lần update (giây)

    private float currentTemperature;
    private Coroutine updateCoroutine;

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
        
        // Start slow update coroutine
        if (updateCoroutine != null)
            StopCoroutine(updateCoroutine);
        updateCoroutine = StartCoroutine(SlowUpdateTemperature());
    }
    [ProButton]
    private void TurnOff() {
        // Show the button
        if (turnOnBtn != null)
            turnOnBtn.SetActive(true);

        // Display default placeholder
        if (valueText != null)
            valueText.text = "----";
        experiment.guideStepManager.ReactivateStep("TURNON_NHIETKE");
        
        // Stop update coroutine
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
            updateCoroutine = null;
        }
    }

    public void Restart() {
        isOn = false;
        TurnOff();
    }
    
    // Coroutine để update nhiệt độ chậm hơn
    private IEnumerator SlowUpdateTemperature()
    {
        string lastDisplayText = "";
        
        while (isOn && experiment != null)
        {
            // Lấy nhiệt độ hiện tại từ Experiment
            currentTemperature = experiment.GetCurrentTemp();
            
            // Cache string để tránh ToString mỗi frame
            string newDisplayText = currentTemperature.ToString("F2");
            
            // Chỉ update text nếu giá trị thay đổi
            if (newDisplayText != lastDisplayText && valueText != null)
            {
                valueText.text = newDisplayText;
                lastDisplayText = newDisplayText;
            }
            
            // Chờ interval trước khi update tiếp
            yield return new WaitForSeconds(updateInterval);
        }
    }
}
