using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class XiLanhController : MonoBehaviour {
    [Header("References")]
    [SerializeField] private Transform pittong;
    [SerializeField] private Transform grabProxy;
    [SerializeField] private HandGrabInteractable grab;
    [SerializeField] private RayInteractable ray;

    [Header("Y Axis Limit")]
    [SerializeField] private float minY = -0.555f;
    [SerializeField] private float maxY = -0.28f;
    [SerializeField] private float returnSpeed = 3f;

    private Vector3 pittongStartLocalPos;
    private Quaternion pittongStartLocalRot;

    private Vector3 proxyStartLocalPos;
    private Quaternion proxyStartLocalRot;

    private bool isGrabbed = false;
    private bool isReturning = false;

    private void Start() {
        pittongStartLocalPos = pittong.localPosition;
        pittongStartLocalRot = pittong.localRotation;

        proxyStartLocalPos = grabProxy.localPosition;
        proxyStartLocalRot = grabProxy.localRotation;

        grab.WhenSelectingInteractorViewAdded += _ => OnGrab();
        grab.WhenSelectingInteractorViewRemoved += _ => OnRelease();
        ray.WhenSelectingInteractorViewAdded += _ => OnGrab();
        ray.WhenSelectingInteractorViewRemoved += _ => OnRelease();
    }

    private void LateUpdate() {
        if (isGrabbed) {
            UpdatePittongByProxy();
        } else if (isReturning) {
            ReturnProxyToPittong();
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

        proxyStartLocalPos = new Vector3(
            proxyStartLocalPos.x,
            proxyStartLocalPos.y + (pittong.localPosition.y - pittongStartLocalPos.y),
            proxyStartLocalPos.z
        );

        grabProxy.localRotation = proxyStartLocalRot;
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

        // Stop returning when close enough
        if (Vector3.Distance(grabProxy.localPosition, targetPos) < 0.001f)
            isReturning = false;
    }
}
