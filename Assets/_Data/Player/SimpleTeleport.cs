using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

public class SimpleTeleport : SingletonCtrl<SimpleTeleport> {
    [Tooltip("OVRCameraRig để di chuyển")]
    public Transform player;

    [Tooltip("Vị trí đích để teleport tới")]
    public Transform targetPosition;

    private Vector3 originalPosition;

    protected override void Start() {
        // Save the starting tracking space position
        originalPosition = player.localPosition;
        Debug.Log($"[SimpleTeleport] Original position saved: {originalPosition}");
    }

    // Call this method to teleport instantly
    [ProButton]
    public void Teleport() {
        if (player == null || targetPosition == null) {
            Debug.LogWarning("[SimpleTeleport] Missing cameraRig or targetPosition reference!");
            return;
        }

        Vector3 beforePos = player.localPosition;

        player.localPosition = new Vector3(
            targetPosition.position.x,
            originalPosition.y,
            targetPosition.position.z
        );

        Vector3 afterPos = player.localPosition;

        Debug.Log($"[SimpleTeleport] Teleported from {beforePos} → {afterPos}");
    }
}
