using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class XiLanhController : MonoBehaviour {
    [Header("References")]
    [SerializeField] private Transform pittong;        // The piston
    [SerializeField] private Transform grabProxy;      // The invisible grab handle
    [SerializeField] private HandGrabInteractable grab;
    [SerializeField] private RayInteractable ray;

    [Header("Experiment")]
    [SerializeField] private Experiment2 experiment;   // Reference to experiment logic

    [Header("Y Axis Limit")]
    public float minY = -0.555f;   // Fully compressed
    public float maxY = -0.28f;    // Fully extended
    public float returnSpeed = 3f;

    private Vector3 pittongStartLocalPos;
    private Quaternion pittongStartLocalRot;

    private Vector3 proxyStartLocalPos;
    private Quaternion proxyStartLocalRot;

    private bool isGrabbed = false;
    private bool isReturning = false;

    public float CurrentVolume { get; private set; } // 0–100 ml
    private float lastVolume;

    private void Start() {
        pittongStartLocalPos = pittong.localPosition;
        pittongStartLocalRot = pittong.localRotation;

        proxyStartLocalPos = grabProxy.localPosition;
        proxyStartLocalRot = grabProxy.localRotation;

        grab.WhenSelectingInteractorViewAdded += _ => OnGrab();
        grab.WhenSelectingInteractorViewRemoved += _ => OnRelease();
        ray.WhenSelectingInteractorViewAdded += _ => OnGrab();
        ray.WhenSelectingInteractorViewRemoved += _ => OnRelease();

        UpdateVolume(); // Initialize
        lastVolume = CurrentVolume;
    }

    private void LateUpdate() {
        if (isGrabbed) {
            UpdatePittongByProxy();
            UpdateVolumeIfChanged();
        } else if (isReturning) {
            ReturnProxyToPittong();
            UpdateVolumeIfChanged();
        }
    }

    private void OnGrab() {
        isGrabbed = true;
        isReturning = false;
    }

    private void UpdatePittongByProxy() {
        float offsetY = grabProxy.localPosition.y - pittongStartLocalPos.y;
        float newY = Mathf.Clamp(offsetY, minY, maxY);

        pittong.localPosition = new Vector3(
            pittongStartLocalPos.x,
            newY,
            pittongStartLocalPos.z
        );
        pittong.localRotation = pittongStartLocalRot;
    }

    private void OnRelease() {
        isGrabbed = false;
        isReturning = true;

        // Check once on release (ensures player finished moving)
        EvaluateActionOnRelease();
    }

    private void ReturnProxyToPittong() {
        Vector3 targetPos = new Vector3(
            proxyStartLocalPos.x,
            proxyStartLocalPos.y + (pittong.localPosition.y - pittongStartLocalPos.y),
            proxyStartLocalPos.z
        );

        grabProxy.localPosition = Vector3.Lerp(
            grabProxy.localPosition,
            targetPos,
            Time.deltaTime * returnSpeed
        );

        grabProxy.localRotation = Quaternion.Lerp(
            grabProxy.localRotation,
            proxyStartLocalRot,
            Time.deltaTime * returnSpeed
        );

        if (Vector3.Distance(grabProxy.localPosition, targetPos) < 0.001f)
            isReturning = false;
    }

    private void UpdateVolume() {
        float normalized = Mathf.InverseLerp(maxY, minY, pittong.localPosition.y);
        CurrentVolume = Mathf.Lerp(0f, 100f, normalized);
    }

    private void UpdateVolumeIfChanged() {
        float previous = lastVolume;
        UpdateVolume();

        if (Mathf.Abs(CurrentVolume - previous) > 0.5f) {
            lastVolume = CurrentVolume;
        }
    }

    private void EvaluateActionOnRelease() {
        // Example thresholds (can adjust depending on actual movement range)
        const float pushThreshold = 20f;   // compressed
        const float releaseThreshold = 80f; // extended

        // Get current guide step to know what player is supposed to do
        var manager = experiment.guideStepManager;
        string stepID = manager.currentStepID;

        if (stepID == "PUSH_XILANH") {
            if (CurrentVolume <= pushThreshold) {
                manager.CompleteStep("PUSH_XILANH");
                Debug.Log("PUSH_XILANH completed");
            } else {
                manager.ReactivateStep("PUSH_XILANH");
                Debug.Log("PUSH_XILANH failed, retrying");
            }
        } else if (stepID == "RELEASE_XILANH") {
            if (CurrentVolume >= releaseThreshold) {
                manager.CompleteStep("RELEASE_XILANH");
                Debug.Log("RELEASE_XILANH completed");
            } else {
                manager.ReactivateStep("RELEASE_XILANH");
                Debug.Log("RELEASE_XILANH failed, retrying");
            }
        }
    }
}
