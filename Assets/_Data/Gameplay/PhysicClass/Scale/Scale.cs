using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Scale : NewMonobehavior {
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private GameController gameController;

    [Header("Runtime")]
    [SerializeField] protected Bottle bottle;
    [SerializeField] private Coroutine delayRoutine;
    public float tempWeight = 0f;

    [Header("UI")]
    [SerializeField] private TextMeshPro valueScale;
    private bool isHaveScale = false;

    [Header("Bottle Weight Settings")]
    [SerializeField] private float cupWeight = 80f;     // Empty cup weight (grams)
    [SerializeField] private float fullVolume = 200f;    // Water volume when full (ml)
    [SerializeField] private float waterDensity = 1f;    // Water density (1 g/ml)

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadBoxCollider();
        this.LoadGameController();
    }

    protected virtual void LoadGameController() {
        if (this.gameController != null) return;

        this.gameController = GameObject.FindAnyObjectByType<GameController>();

    }
    protected virtual void LoadBoxCollider() {
        if (this.boxCollider != null) return;
        this.boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.center = new Vector3(0.009896755f, 0.4412669f, -0.2078676f);
        boxCollider.size = new Vector3(1.494927f, 0.4951394f, 1.455317f);
    }

    protected virtual void LoadUI() { 
        if(this.valueScale != null) return;
        this.valueScale = GetComponentInChildren<TextMeshPro>();
    }

    private void OnTriggerEnter( Collider other ) {
        Bottle target = other.GetComponent<Bottle>();
        if (target == null) return;

        bottle = target;
        isHaveScale = false;
        if (delayRoutine != null)
            StopCoroutine(delayRoutine);

        delayRoutine = StartCoroutine(ShowWeightAfterDelay(0.5f));
    }

    private IEnumerator ShowWeightAfterDelay( float delay ) {
        // Wait for the given delay
        yield return new WaitForSeconds(delay);

        if (bottle == null) yield break;

        // Get the current fill ratio of the bottle
        float fillRatio = bottle.CurrentLiquid / bottle.MaxLiquid;

        // Calculate current water volume (ml)
        float currentVolume = fullVolume * fillRatio;

        // Calculate water weight (grams)
        float waterWeight = currentVolume * waterDensity;

        // Calculate total weight (cup + water)
        float totalWeight = cupWeight + waterWeight;

        // Display the result on UI
        if (valueScale != null)
            valueScale.text = $"{totalWeight:F0}";

        tempWeight = waterWeight;

        Debug.Log($"[Scale] Bottle detected — Water: {currentVolume:F1} ml, Weight: {totalWeight:F1} g");
    }

    private void OnTriggerExit( Collider other ) {
        if (bottle == null || other.gameObject != bottle.gameObject) return;

        if (delayRoutine != null)
            StopCoroutine(delayRoutine);
        CompleteScaled();
        bottle = null;

    }
    public void ResetDisplay() {
        if (valueScale != null)
            valueScale.text = "0";
    }

    public string GetDisplayValue() {
        return valueScale != null ? valueScale.text : "0";
    }

    public void CompleteScaled() {
        if (isHaveScale) return;
        isHaveScale = true;
        GuideStepManager.Instance.CompleteStep("CHECK_SCALE");
    }



}
