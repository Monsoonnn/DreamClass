using com.cyborgAssets.inspectorButtonPro;
using Oculus.Interaction.HandGrab;
using UnityEngine;

public class ForceHand : NewMonobehavior
{

    [Header("Interactions")]
    public HandGrabInteractor handInteractor;
    public HandGrabInteractable handInteractable;


    protected override void LoadComponents() {
        base.LoadComponents();
        LoadObjectInteractable();
    }

    protected virtual void LoadObjectInteractable() {
        if(handInteractable != null) return;
        handInteractable = GetComponentInChildren<HandGrabInteractable>();
    }


    [ProButton]
    public void AttachToHand() {
        handInteractor = GameObject.FindAnyObjectByType<HandGrabInteractor>();
        if (handInteractor == null || handInteractable == null) {
            Debug.LogWarning("Interactor or Interactable not assigned");
            return;
        }
        handInteractor.ForceSelect(handInteractable, true);


    }
    [ProButton]
    public void DetachFromHand() {
        handInteractor = GameObject.FindAnyObjectByType<HandGrabInteractor>();
        if (handInteractor == null || handInteractable == null) {
            Debug.LogWarning("Interactor or Interactable not assigned");
            return;
        }
        handInteractor.ForceRelease();
    }
}
