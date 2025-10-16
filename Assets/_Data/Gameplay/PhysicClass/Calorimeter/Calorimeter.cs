using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class Calorimeter : NewMonobehavior {
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private Transform holdBottlePosition;

    [Header("Stored Bottle")]
    [SerializeField] private Bottle bottle; // The permanent bottle stored inside
    public Bottle Bottle => bottle;

    [Header("Temporary Bottle")]
    [SerializeField] private Bottle tempBottle; // The temporary bottle currently inside the trigger

    [Header("Timing")]
    [SerializeField] private float stayDuration = 2f; // Time required to confirm placement
    private Coroutine checkStayRoutine;

    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private GameObject canvas;
    [SerializeField] private GameObject loadingText;
    [SerializeField] private GameObject successText;
    [SerializeField] private GameObject buttonGetBottleOut;

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadBoxCollider();
        this.LoadHoldPosition();
    }

    protected virtual void LoadBoxCollider() {
        if (boxCollider != null) return;
        boxCollider = transform.GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector3(1.25f, 2f, 1.25f);
    }

    protected virtual void LoadHoldPosition() {
        if (holdBottlePosition != null) return;
        holdBottlePosition = transform.Find("HoldBottlePos");
    }

    private void OnTriggerEnter( Collider other ) {
        //Debug.Log("OnTriggerEnter");

        Bottle target = other.GetComponent<Bottle>();
        if (target == null) return;

        // If a bottle is already stored -> show button only
        if (bottle != null) {
            ShowStoredBottleUI();
            return;
        }

        // Assign temp bottle and start countdown
        tempBottle = target;
        if (canvas != null) canvas.SetActive(true);

        if (checkStayRoutine != null)
            StopCoroutine(checkStayRoutine);

        checkStayRoutine = StartCoroutine(CheckBottleStayCoroutine());
    }

    private void OnTriggerExit( Collider other ) {
       /* Debug.Log("OnTriggerExit");*/

        if (tempBottle == null || other.gameObject != tempBottle.gameObject) return;

        // Cancel current countdown
        if (checkStayRoutine != null) {
            StopCoroutine(checkStayRoutine);
            checkStayRoutine = null;
        }

        HideAllText();
        tempBottle = null;
    }

    private IEnumerator CheckBottleStayCoroutine() {
        ShowUIState(loading: true);

        float timer = 0f;

        while (timer < stayDuration && tempBottle != null) {
            timer += Time.deltaTime;
            if (slider != null)
                slider.value = timer / stayDuration;
            yield return null;
        }

        // If bottle stayed long enough -> confirm placement
        if (tempBottle != null) {
            bottle = tempBottle;
            tempBottle = null;
            SetBottle();
            ShowUIState(success: true);
            GuideStepManager.Instance.CompleteStep("PLACEON_BINHDO");
            Debug.Log("Bottle stored successfully!");
            HideAllText();
        }
        yield return new WaitForSeconds(1.5f);
        checkStayRoutine = null;
    }

    protected virtual void SetBottle() { 
        bottle.forceHand.DetachFromHand();
        bottle.transform.position = holdBottlePosition.position;
        bottle.transform.rotation = holdBottlePosition.rotation;
        bottle.gameObject.SetActive(false);
    }



    private void ShowUIState( bool loading = false, bool success = false ) {
        if (loadingText != null) loadingText.SetActive(loading);
        if (successText != null) successText.SetActive(success);
        if (buttonGetBottleOut != null) buttonGetBottleOut.SetActive(success);
    }

    private void ShowStoredBottleUI() {
        if (canvas != null) canvas.SetActive(false);
        if (buttonGetBottleOut != null) buttonGetBottleOut.SetActive(true);
        if (loadingText != null) loadingText.SetActive(false);
        if (successText != null) successText.SetActive(false);
    }

    private void HideAllText() {
        if (canvas != null) canvas.SetActive(false);
        if (loadingText != null) loadingText.SetActive(false);
        if (successText != null) successText.SetActive(false);
    }

    public void GetBottle() {
        // Allow user to get the stored bottle out
        if (bottle == null) return;
        Debug.Log("Get bottle");
        bottle.gameObject.SetActive(true);
        bottle.forceHand.AttachToHand();
        bottle = null; // Remove reference
        HideAllText();
    }
    public void HideAllUI() {
        if (canvas != null) canvas.SetActive(false);
        if (loadingText != null) loadingText.SetActive(false);
        if (successText != null) successText.SetActive(false);
        if (buttonGetBottleOut != null) buttonGetBottleOut.SetActive(false);
    }

    public void ClearBottle() {
        bottle = null;
        tempBottle = null;
    }

}
