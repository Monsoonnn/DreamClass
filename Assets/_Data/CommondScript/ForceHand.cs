using com.cyborgAssets.inspectorButtonPro;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

public class ForceHand : NewMonobehavior {
    [Header("Interactions")]
    public HandGrabInteractor handInteractor;
    public GrabInteractor controllerInteractor;

    public HandGrabInteractable handInteractable;
    public GrabInteractable grabInteractable;

    [Header("Settings")]
    public float flySpeed = 10f; // speed for flying to hand

    protected override void LoadComponents() {
        base.LoadComponents();
        LoadObjectInteractable();
        LoadGrabInteractable();
        LoadControllerInteractor();
        LoadHandInteractor();
    }

    protected virtual void LoadGrabInteractable() {
        if (grabInteractable != null) return;
        grabInteractable = GetComponentInChildren<GrabInteractable>();
    }

    protected virtual void LoadObjectInteractable() {
        if (handInteractable != null) return;
        handInteractable = GetComponentInChildren<HandGrabInteractable>();
    }

    protected virtual void LoadControllerInteractor() {
        if (controllerInteractor != null) return;
        controllerInteractor = GameObject.FindAnyObjectByType<GrabInteractor>();
    }

    protected virtual void LoadHandInteractor() {
        if (handInteractor != null) return;
        handInteractor = GameObject.FindAnyObjectByType<HandGrabInteractor>();
    }

    [ProButton]
    public void AttachToHand() {
        LoadHandInteractor();
        LoadControllerInteractor();

        if (IsHandTrackingActive()) {
            if (handInteractor == null || handInteractable == null) {
                Debug.LogWarning("[ForceHand] Missing hand interactor or interactable!");
                return;
            }

            handInteractor.ForceSelect(handInteractable, true);
            Debug.Log("[ForceHand] Attached via Hand Tracking");
        } else if (IsControllerActive()) {
            if (controllerInteractor == null || grabInteractable == null) {
                Debug.LogWarning("[ForceHand] Missing controller interactor or grab interactable!");
                return;
            }

            StartCoroutine(MoveAndAttachToController());
        } else {
            Debug.LogWarning("[ForceHand] No active interactor detected!");
        }
    }

    private System.Collections.IEnumerator MoveAndAttachToController() {
        Transform target = controllerInteractor.transform; // the grab point on controller
        Transform obj = grabInteractable.transform.parent;

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true; // prevent physics interference
        }

        // Fly smoothly toward controller
        while (Vector3.Distance(obj.position, target.position) > 0.05f) {
            obj.position = Vector3.MoveTowards(obj.position, target.position, flySpeed * Time.deltaTime);
            obj.rotation = Quaternion.RotateTowards(obj.rotation, target.rotation, flySpeed * 20f * Time.deltaTime);
            yield return null;
        }

        // Attach once reached
        controllerInteractor.ForceSelect(grabInteractable);
        Debug.Log("[ForceHand] Attached via Controller (with fly)");

        controllerInteractor.ClearComputeCandidateOverride();
        controllerInteractor.ClearComputeShouldSelectOverride();
        controllerInteractor.ClearComputeShouldUnselectOverride();
    }

    [ProButton]
    public void DetachFromHand() {
        LoadHandInteractor();
        LoadControllerInteractor();

        if (IsHandTrackingActive() && handInteractor != null) {
            handInteractor.ForceRelease();
            grabInteractable.Rigidbody.isKinematic = false;
            Debug.Log("[ForceHand] Detached HandGrabInteractor");
        } else if (IsControllerActive() && controllerInteractor != null) {

            controllerInteractor.ForceRelease();
            grabInteractable.Rigidbody.isKinematic = false;
            Debug.Log("[ForceHand] Detached GrabInteractor");
        } else {
            Debug.LogWarning("[ForceHand] No active interactor to detach!");
        }
    }

    [ProButton]
    public void DetachItem() {
        LoadHandInteractor();
        LoadControllerInteractor();

        if (IsHandTrackingActive() && handInteractor != null) {
            handInteractor.ForceRelease();
            Debug.Log("[ForceHand] Detached HandGrabInteractor");
        } else if (IsControllerActive() && controllerInteractor != null) {
            controllerInteractor.ForceRelease();
            Debug.Log("[ForceHand] Detached GrabInteractor");
        } else {
            Debug.LogWarning("[ForceHand] No active interactor to detach!");
        }
    }


    private bool IsHandTrackingActive() {
        return handInteractor != null && handInteractor.gameObject.activeInHierarchy;
    }

    private bool IsControllerActive() {
        return controllerInteractor != null && controllerInteractor.gameObject.activeInHierarchy;
    }
}
