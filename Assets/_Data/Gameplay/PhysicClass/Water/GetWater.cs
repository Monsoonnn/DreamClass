using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(BoxCollider))]
public class GetWater : NewMonobehavior {
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private Slider slider;
    [SerializeField] private GameController gameController;

    [Header("Runtime")]
    [SerializeField] private Bottle bottle;

    [Header("Timing")]
    [SerializeField] private float fillDuration = 5f; // Time to fill the bottle completely
    private Coroutine fillRoutine;
    private bool isHaveWater = false;

    [Header("UI")]
    [SerializeField] private GameObject canvas;
    [SerializeField] private GameObject loadingText; 
    [SerializeField] private GameObject successText; 
    [SerializeField] private GameObject fullText;    

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadBoxCollider();
        this.LoadSlider();
        this.LoadGameController();
    }

    protected virtual void LoadBoxCollider() {
        if (boxCollider != null) return;
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
    }

    protected virtual void LoadGameController() {
        if (this.gameController != null) return;

        this.gameController = GameObject.FindAnyObjectByType<GameController>();

    }

    protected virtual void LoadSlider() {
        if (slider != null) return;
        canvas = this.transform.Find("Canvas").gameObject;
        if (canvas != null)
            slider = canvas.GetComponentInChildren<Slider>();
    }

    private void OnTriggerEnter( Collider other ) {
        Debug.Log("OnTriggerEnter");
        
        Bottle target = other.GetComponent<Bottle>();
        if (target == null) return;
        if (canvas != null) canvas.SetActive(true);
        bottle = target;

        // Stop previous coroutine if active
        if (fillRoutine != null)
            StopCoroutine(fillRoutine);

        // If bottle is already full
        if (bottle.CurrentLiquid >= bottle.MaxLiquid) {
            ShowUIState(full: true);
            return;
        }

        if (bottle.CurrentLiquid == 0) isHaveWater = false;

        // Start filling
        fillRoutine = StartCoroutine(FillBottleCoroutine());
    }

    private void OnTriggerExit( Collider other ) {
        Debug.Log("OnTriggerExit");
        if (bottle == null || other.gameObject != bottle.gameObject) return;

        if (fillRoutine != null) {
            StopCoroutine(fillRoutine);
            fillRoutine = null;
        }

        // Hide all text when leaving the zone
        HideAllText();
        HaveWater();

        bottle = null;
    }

    private void HaveWater() {
        if (isHaveWater) return;
        GuideStepManager.Instance.CompleteStep("GET_WATER");
        isHaveWater = true;
    }


    private IEnumerator FillBottleCoroutine() {
        ShowUIState(loading: true);

        float startLiquid = bottle.CurrentLiquid;
        float startRatio = startLiquid / bottle.MaxLiquid;
        float elapsed = startRatio * fillDuration;

        while (elapsed < fillDuration && bottle != null) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fillDuration);
            float newLiquid = bottle.MaxLiquid * t;

            bottle.UpdateLiquidLevel(newLiquid);

            if (slider != null)
                slider.value = t;

            yield return null;
        }

        // Ensure the bottle is full at the end
        if (bottle != null)
            bottle.UpdateLiquidLevel(bottle.MaxLiquid);

        ShowUIState(success: true);
        fillRoutine = null;

        // Wait 2 seconds then hide text
        yield return new WaitForSeconds(2f);
        HideAllText();
    }

    private void ShowUIState( bool loading = false, bool success = false, bool full = false ) {
        if (loadingText != null) loadingText.SetActive(loading);
        if (successText != null) successText.SetActive(success);
        if (fullText != null) fullText.SetActive(full);
    }

    private void HideAllText() {
        if (canvas != null) canvas.SetActive(false);
        if (loadingText != null) loadingText.SetActive(false);
        if (successText != null) successText.SetActive(false);
        if (fullText != null) fullText.SetActive(false);
    }
    public void HideAllUI() {
        if (canvas != null) canvas.SetActive(false);
        if (loadingText != null) loadingText.SetActive(false);
        if (successText != null) successText.SetActive(false);
        if (fullText != null) fullText.SetActive(false);
    }

}
