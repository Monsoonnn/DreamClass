using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using Oculus.Interaction; // optional nếu bạn dùng Meta SDK

public class SimpleTeleport : SingletonCtrl<SimpleTeleport> {
    [Tooltip("OVRCameraRig root (parent of TrackingSpace)")]
    public OVRCameraRig cameraRig;

    [Tooltip("Transform đích teleport đến")]
    public Transform targetPosition;

    private Vector3 trackingOffset; // offset của camera bên trong rig
    private Transform trackingSpace;

    protected override void Start() {
        base.Start();

        if (cameraRig == null) {
            cameraRig = FindObjectOfType<OVRCameraRig>();
        }

        trackingSpace = cameraRig.trackingSpace;
        trackingOffset = trackingSpace.localPosition;

        Debug.Log($"[SimpleTeleport] Tracking offset saved: {trackingOffset}");
    }

    [ProButton]
    public void Teleport() {
        if (cameraRig == null || targetPosition == null) {
            Debug.LogWarning("[SimpleTeleport] Missing references!");
            return;
        }

        Transform playerRoot = cameraRig.transform;

        // Tính offset giữa camera thực tế và rig root
        Vector3 cameraWorldPos = cameraRig.centerEyeAnchor.position;
        Vector3 offset = playerRoot.position - cameraWorldPos;

        // Teleport toàn bộ rig sao cho camera đặt tại vị trí target
        Vector3 newRootPos = targetPosition.position + offset;

        // Giữ nguyên Y gốc của player
        newRootPos.y = playerRoot.position.y;

        playerRoot.position = newRootPos;
/*
        // Reset tracking space về offset ban đầu (nếu có)
        if (trackingSpace != null)
            trackingSpace.localPosition = trackingOffset;*/

        Debug.Log($"[SimpleTeleport] Teleported player to {targetPosition.position} (keeping Y), camera aligned.");
    }

}
