using UnityEngine;

public class LookAtCamera : NewMonobehavior {
    [SerializeField] protected Transform playerCamera;
    [SerializeField] protected bool isUIElement = false; 
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

    private void OnEnable() {
        if (playerCamera == null) this.LoadCamera();
    }

    void LateUpdate() {
        if (playerCamera == null) return;

        // Keep original Y rotation only
        Vector3 direction = playerCamera.position - transform.position;
        direction.y = 0; // Lock vertical rotation

        if (direction.sqrMagnitude > 0.001f) {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

           
            if (isUIElement) {
                targetRotation *= Quaternion.Euler(0, 180, 0);
            }

            transform.rotation = targetRotation;
        }
    }
}