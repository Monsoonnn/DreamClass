using UnityEngine;

public class OVRCameraHeight : MonoBehaviour {
    public bool useRealHeight = true;
    public float simulatedHeight = 1.75f;

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
/*            // Reset to default, HMD height will drive camera
            cameraRig.trackingSpace.localPosition = Vector3.zero;*/
/*            Debug.Log("Using real HMD height");*/
        } else {
            // Apply simulated height for seated/simulator mode
            cameraRig.trackingSpace.localPosition = new Vector3(0f, simulatedHeight, 0f);

           /* Debug.Log("Simulated player height: " + simulatedHeight + " m");*/
        }
    }
}
