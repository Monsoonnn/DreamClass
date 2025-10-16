using UnityEngine;

public class ACelectric : NewMonobehavior {
    public bool IsTurnOn = false;
    public InteractionUICtrl interactionUICtrl;

    [Header("UI")]
    [SerializeField] private GameObject turnOn;
    [SerializeField] private GameObject turnOff;
    [SerializeField] private GameObject title;

    [Header("Power Source Settings")]
    public float outputVoltage = 12f;   // Voltage when ON (Volts)
    public float outputCurrent = 1.5f;  // Current when ON (Amperes)
    public GameController gameController; // Reference to GameController

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadGameController();
    }

    protected virtual void LoadGameController() {
        if(this.gameController != null) return;

        this.gameController = GameObject.FindAnyObjectByType<GameController>();

    }

    protected override void Start() {
        IsTurnOn = false;
        ChangeStatus(IsTurnOn);
    }

    // Called when clicking the button
    public virtual void ChangeStatus( bool isTurnOn ) {
        IsTurnOn = isTurnOn;

        if (IsTurnOn) {
            // Power ON: show proper UI
            title.SetActive(true);
            turnOn.SetActive(false);
            turnOff.SetActive(true);

            // Update GameController with voltage/current
            if (gameController != null) {
                gameController.voltage = outputVoltage;
                gameController.current = outputCurrent;
                gameController.CalculatePower();
            }
            GuideStepManager.Instance.CompleteStep("TURNON_AC");
            Debug.Log($"AC Power ON: {outputVoltage}V, {outputCurrent}A");
        } else {
            // Power OFF: reset values and UI
            turnOn.SetActive(true);
            turnOff.SetActive(false);
            title.SetActive(false);

            if (gameController != null) {
                gameController.voltage = 0f;
                gameController.current = 0f;
                gameController.CalculatePower();
            }
            GuideStepManager.Instance.ActivateStep("TURNON_AC");
            Debug.Log("AC Power OFF");
        }
    }
}
