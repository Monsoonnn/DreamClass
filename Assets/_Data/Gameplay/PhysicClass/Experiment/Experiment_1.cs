using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using System.Collections;

public class Experiment: GameController {
    [Header("Experiment State")]
    public bool isStart = false;
    private Coroutine heatingCoroutine;
    
    [Header("Experiment Objects")]
    public Multimeter multimeter;
    public ACelectric acelectric;
    public Calorimeter calorimeter;
    public Bottle bottle;
    public GetWater waterSource;
    public Scale scale;
    public Thermometer thermometer;

    [Header("Result Table")]
    public ResultBook resultBook;

    [Header("Electrical Parameters")]
    public float voltage = 0f;   // in Volts
    public float current = 0f;   // in Amperes
    public float power = 0f;     // in Watts

    [Header("Thermal Parameters")]
    private float waterMass = 0.2f;          // kg
    private float specificHeat = 4200f;      // J/kg°C
    private float environmentTemp = 25f;     // °C
    private float heatLossK = 0.01f;         // heat loss coefficient
    private float currentTemp;              // current water temperature
    private float timeElapsed;              // elapsed experiment time

    [Header("Initial Setup")]
    [SerializeField] private float initialBottleLiquid = 0f;
    [SerializeField] private float initialTemperature = 25f;
    
    protected override void Start() {
        SetupExperiment();
    }

    [ProButton]
    public void SetupExperiment() {
        Debug.Log("Setting up experiment...");
        // Reset all states
        isStart = false;
        voltage = 0f;
        current = 0f;
        power = 0f;
        timeElapsed = 0f;
        currentTemp = initialTemperature;

        // Reset all devices
        if (bottle != null) {
            bottle.UpdateLiquidLevel(initialBottleLiquid);
            if (bottle.forceHand != null)
                bottle.forceHand.DetachFromHand();
        }

        if (calorimeter != null) {
            calorimeter.HideAllUI();
            calorimeter.ClearBottle();
        }

        if (waterSource != null) {
            waterSource.HideAllUI();
            waterSource.SetIsHaveWater(false); 
        }
            

        if (scale != null)
            scale.ResetDisplay();

        if (multimeter != null) {
            multimeter.UpdateDisplay(0);
            multimeter.ResetStep();
        }
           

        if (thermometer != null)
            thermometer.valueText.text = $"{currentTemp:F2}°C";
        GuideStepManager.Instance.ActivateStep(0);

        Debug.Log("Experiment ready — waiting to start.");
    }
    [ProButton]
    public void CalculatePower() {
        if (!isStart) {
            Debug.LogWarning("Experiment not started yet!");
            return;
        }

        power = voltage * current;
        Debug.Log($"Power: {power} W (U={voltage}V, I={current}A)");

        if (multimeter != null)
            multimeter.UpdateDisplay(power);
    }

    [ProButton]
    public override void StartExperiment() {
        if (isStart) return;

        // Ensure bottle exists in calorimeter
        if (calorimeter == null || calorimeter.Bottle == null) {
            Debug.LogWarning("No bottle in calorimeter!");
            return;
        }

        // Get the current bottle
        bottle = calorimeter.Bottle;

        // --- NEW: Get water mass from Scale reading ---
        float measuredWeight = GetMeasuredWaterMassFromScale(); // returns kg

        if (measuredWeight <= 0f) {
            Debug.LogWarning("Scale has no valid measurement — please weigh the bottle first!");
            return;
        }

        waterMass = measuredWeight;

        // Start experiment
        isStart = true;
        timeElapsed = 0f;
        power = voltage * current;

        if (multimeter != null)
            multimeter.UpdateDisplay(power);

        Debug.Log($"Experiment started! Water mass = {waterMass:F3} kg, initial temp = {currentTemp:F2}°C");
        if(resultBook != null) resultBook.AddResult(1, 25, power);

        heatingCoroutine = StartCoroutine(SimulateHeating());
    }


    [ProButton]
    public override void StopExperiment() {
        if (!isStart) return;

        isStart = false;
        if (heatingCoroutine != null) {
            StopCoroutine(heatingCoroutine);
            heatingCoroutine = null;
        }
        SetupExperiment();
        resultBook.ClearResults();

        Debug.Log("Experiment stopped!");
    }

    /// <summary>
    /// Coroutine to simulate water heating over time
    /// </summary>
    [ProButton]
    private IEnumerator SimulateHeating() {
        float simulationStep = 0.1f;     // Simulation step time (seconds)
        float recordInterval = 5f;      // Time interval to record data to ResultBook
        float nextRecordTime = recordInterval;
        float totalSimulationTime = 180f; // 3 minutes (adjustable)

        // Ensure the power display is initialized
        if (multimeter != null)
            multimeter.UpdateDisplay(power);

        while (isStart) {
            timeElapsed += simulationStep;

            // Stop condition
            if (timeElapsed >= totalSimulationTime) {
                isStart = false;
                Debug.Log($"[SimulateHeating] Simulation finished after {timeElapsed:F1}s.");
                if (heatingCoroutine != null) {
                    StopCoroutine(heatingCoroutine);
                    heatingCoroutine = null;
                }
                yield break;
            }

            // Random power fluctuation ±5%
            float fluctuation = Random.Range(-0.05f, 0.05f);
            float currentPower = power * (1f + fluctuation);

            // dT/dt = (P/mc) - k(T - T_env)
            float dTdt = (currentPower / (waterMass * specificHeat)) - heatLossK * (currentTemp - environmentTemp);

            // Add a small random scaling to simulate instability
            float simulationScale = Random.Range(0.01f, 0.1f);

            // Compute temperature change for this step
            float dT = dTdt + simulationScale;
            currentTemp += dT;

            // Add result 
            if (timeElapsed >= nextRecordTime) {
                // Update thermometer UI
                if (thermometer != null)
                    thermometer.valueText.text = $"{currentTemp:F2}°C";

                // Update multimeter UI
                if (multimeter != null)
                    multimeter.UpdateDisplay(currentPower);

              
                if (resultBook != null ) {
                    resultBook.AddResult(timeElapsed, currentTemp, currentPower);
                    nextRecordTime += recordInterval; // Schedule next record time
                }
            }

            // Log to console
            //Debug.Log($"t={timeElapsed:F1}s | P={currentPower:F2}W | T={currentTemp:F2}°C");

            yield return new WaitForSeconds(simulationStep);
        }
    }




    public float GetMeasuredWaterMassFromScale() {
        if (scale == null) {
            Debug.LogWarning("Scale not assigned!");
            return 0f;
        }

        // Read displayed text on the scale
        if (float.TryParse(scale.GetDisplayValue(), out float totalWeightGrams)) {
            float waterMassKg = scale.tempWeight / 1000f;
            Debug.Log($"[GameController] Water mass detected from scale: {waterMassKg:F3} kg");
            return waterMassKg;
        }

        Debug.LogWarning("Invalid scale reading!");
        return 0f;
    }

}
