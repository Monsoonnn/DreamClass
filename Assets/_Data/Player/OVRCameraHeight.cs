using UnityEngine;

public class OVRCameraHeight : MonoBehaviour {
    [Header("Height Settings")]
    public bool useRealHeight = true;
    public float simulatedHeight = 1.75f;

    [Header("Height Limits")]
    public bool limitHeight = true;        // Enable height clamping
    public float minAllowedHeight = 1.4f;  // Minimum allowed height
    public float maxAllowedHeight = 1.8f;  // Maximum allowed height

    private OVRCameraRig cameraRig;

    void Start() {
        cameraRig = GetComponent<OVRCameraRig>();

        if (cameraRig == null) {
            Debug.LogError("OVRCameraRig not found under player!");
            return;
        }
    }

    void Update() {
        AdjustHeight();
    }

    public void AdjustHeight() {
        if (cameraRig == null) return;

        if (useRealHeight) {
            // Real HMD mode
            Transform centerEye = cameraRig.centerEyeAnchor;
            if (centerEye != null && limitHeight) {
                float currentHeight = centerEye.localPosition.y;
                float clampedHeight = Mathf.Clamp(currentHeight, minAllowedHeight, maxAllowedHeight);

                // Adjust tracking space to keep head within bounds
                float offset = clampedHeight - currentHeight;
                cameraRig.trackingSpace.localPosition += new Vector3(0f, offset, 0f);
            }
        } else {
            // Simulated mode
            float clampedSimHeight = simulatedHeight;

            if (limitHeight)
                clampedSimHeight = Mathf.Clamp(simulatedHeight, minAllowedHeight, maxAllowedHeight);

            cameraRig.trackingSpace.localPosition = new Vector3(0f, clampedSimHeight, 0f);
        }
    }
}
