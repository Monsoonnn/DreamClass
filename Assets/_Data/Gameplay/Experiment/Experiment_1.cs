using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using System.Collections;

public class Experiment : GameController
{
    private Coroutine heatingCoroutine;

    [Header("Experiment Objects")]
    public Multimeter multimeter;
    public ACelectric acelectric;
    public Calorimeter calorimeter;
    public WaterCup waterCup;           // Changed from Bottle
    public WaterStream waterStream;     // Changed from GetWater
    public Scale scale;
    public Thermometer thermometer;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startExperimentSound;

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
    [SerializeField] private float initialWaterAmount = 0f;     // Changed from initialBottleLiquid
    [SerializeField] private float initialTemperature = 25f;

    protected override void Start()
    {
        //SetupExperiment();
    }

    [ProButton]
    public override void SetupExperiment()
    {
        base.SetupExperiment();

        Debug.Log($"[Experiment] Setup experiment: {GetExperimentName()}");
        // Reset all states
        isExperimentRunning = false;
        voltage = 0f;
        current = 0f;
        power = 0f;
        timeElapsed = 0f;
        currentTemp = initialTemperature;

        // Reset water cup
        if (waterCup != null)
        {
            waterCup.SetAmount(initialWaterAmount);
            waterCup.SetIsHaveWater(false);
            waterCup.gameObject.SetActive(true);
        }

        // Reset water stream
        if (waterStream != null)
        {
            waterStream.StopFlow();
            waterStream.SetFlowing(true);
        }

        if (calorimeter != null)
        {
            calorimeter.HideAllUI();
            calorimeter.ClearBottle();
        }

        if (scale != null)
            scale.ResetDisplay();

        if (multimeter != null)
        {
            multimeter.UpdateDisplay(0);
            multimeter.ResetStep();
        }

        if (thermometer != null)
            thermometer.valueText.text = $"{currentTemp:F2}°C";
            
        GuideStepManager.Instance.ActivateStep(0);
        isExperimentRunning = false;

        
    }

    [ProButton]
    public void CalculatePower()
    {
        if (!isExperimentRunning)
        {
            Debug.LogWarning("Experiment not started yet!");
            return;
        }

        power = voltage * current;
        Debug.Log($"Power: {power} W (U={voltage}V, I={current}A)");

        if (multimeter != null)
            multimeter.UpdateDisplay(power);
    }

    [ProButton]
    public override void StartExperiment()
    {
        if (isExperimentRunning) return;

        // Ensure water cup exists and has water
        if (waterCup == null)
        {
            Debug.LogWarning("No water cup assigned!");
            return;
        }

        if (waterCup.IsEmpty())
        {
            Debug.LogWarning("Water cup is empty! Please fill it first.");
            return;
        }

        // Get water mass from Scale reading
        float measuredWeight = GetMeasuredWaterMassFromScale(); // returns kg

        if (measuredWeight <= 0f)
        {
            Debug.LogWarning("Scale has no valid measurement – please weigh the water cup first!");
            return;
        }

        waterMass = measuredWeight;
        timeElapsed = 0f;
        power = voltage * current;

        if (multimeter != null)
            multimeter.UpdateDisplay(power);
        
        // Play start experiment sound
        if (audioSource != null && startExperimentSound != null)
        {
            audioSource.clip = startExperimentSound;
            audioSource.Play();
        }

        Debug.Log($"Experiment started! Water mass = {waterMass:F3} kg, initial temp = {currentTemp:F2}°C");
        
        if (resultBook != null) 
            resultBook.AddResult(1, currentTemp, power);
        isExperimentRunning = true;
        heatingCoroutine = StartCoroutine(SimulateHeating());
       
        base.StartExperiment();
    }

    [ProButton]
    public override void StopExperiment()
    {
        
        if (heatingCoroutine != null)
        {
            StopCoroutine(heatingCoroutine);
            heatingCoroutine = null;
        }
        
        // Stop audio
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        SetupExperiment();
        
        if (resultBook != null)
            resultBook.ClearResults();
            
        base.StopExperiment();
        Debug.Log("Experiment stopped!");
    }

    /// <summary>
    /// Coroutine to simulate water heating over time
    /// </summary>
    public float totalSimulationTime = 180f; // 3 minutes (adjustable)
    
    [ProButton]
    private IEnumerator SimulateHeating()
    {
        float simulationStep = 0.1f;     // Simulation step time (seconds)
        float recordInterval = 2.5f;       // Time interval to record data to ResultBook
        float nextRecordTime = recordInterval;

        // Ensure the power display is initialized
        if (multimeter != null)
            multimeter.UpdateDisplay(power);

        while (isExperimentRunning)
        {
            timeElapsed += simulationStep;

            // Stop condition
            if (timeElapsed >= totalSimulationTime)
            {
                isExperimentRunning = false;
                NotifyExperimentCompleted();
                Debug.Log($"[SimulateHeating] Simulation finished after {timeElapsed:F1}s.");
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

            // Record data at intervals
            if (timeElapsed >= nextRecordTime)
            {
                // Update thermometer UI
                if (thermometer != null)
                    thermometer.valueText.text = $"{currentTemp:F2}°C";

                // Update multimeter UI
                if (multimeter != null)
                    multimeter.UpdateDisplay(currentPower);

                if (resultBook != null)
                {
                    resultBook.AddResult(timeElapsed, currentTemp, currentPower);
                    nextRecordTime += recordInterval; // Schedule next record time
                }
            }

            yield return new WaitForSeconds(simulationStep);
        }
    }

    public float GetMeasuredWaterMassFromScale()
    {
        if (scale == null)
        {
            Debug.LogWarning("Scale not assigned!");
            return 0f;
        }

        // Read displayed text on the scale
        if (float.TryParse(scale.GetDisplayValue(), out float totalWeightGrams))
        {
            float waterMassKg = scale.tempWeight / 1000f;
            Debug.Log($"[GameController] Water mass detected from scale: {waterMassKg:F3} kg");
            return waterMassKg;
        }

        Debug.LogWarning("Invalid scale reading!");
        return 0f;
    }

    // Helper method to check if water cup is ready for experiment
    public bool IsWaterCupReady()
    {
        if (waterCup == null) return false;
        
        // Check if cup has enough water (at least 50% full)
        return waterCup.GetFillPercentage() >= 0.5f;
    }

    // Method to get current water amount from cup
    public float GetWaterAmount()
    {
        if (waterCup == null) return 0f;
        return waterCup.GetCurrentAmount();
    }

    public override string GetExperimentName()
    {
        return "NHIET_DUNG_NUOC";
    }

    public override bool IsExperimentRunning()
    {
        return isExperimentRunning;
    }
}