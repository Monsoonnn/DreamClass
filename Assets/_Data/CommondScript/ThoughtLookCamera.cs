using UnityEngine;

public class LookAtCamera : NewMonobehavior {

    [SerializeField] protected Transform playerCamera;
    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadCamera();
    }

    protected virtual void LoadCamera() {
        if (this.playerCamera != null) return;
        Camera mainCamera = Camera.main;
        if (mainCamera != null) {
            this.playerCamera = mainCamera.transform;
            Debug.Log(transform.name + ": LoadCamera", gameObject);
        } else {
            Debug.LogWarning(transform.name + ": No active camera found!", gameObject);
        }
    }

    void LateUpdate() {
        
        transform.LookAt(playerCamera);

        
        transform.rotation = Quaternion.LookRotation(transform.position - playerCamera.position);
    }
}