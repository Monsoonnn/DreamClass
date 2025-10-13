using UnityEngine;

public class CanvasFollow : MonoBehaviour {

    private bool tracking = false;
    public float distance = 2.25f;

    void OnEnable() {
        tracking = true;
    }

    void OnDisable() {
        tracking = false;
    }

    void LateUpdate() {
        if (!tracking) return;

        Camera activeCam = Camera.main;

        if (activeCam != null) {
            Vector3 forward = activeCam.transform.forward;
            Vector3 position = activeCam.transform.position + forward * distance;

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(forward, activeCam.transform.up);
        }
    }
}
