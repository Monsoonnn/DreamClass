using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

public class RespawnPoint : MonoBehaviour {
    [Tooltip("Điểm đích teleport đến khi respawn")]
    [SerializeField] private Transform respawnTarget;

    protected void Reset() {
        // Default target là chính object này
        if (respawnTarget == null)
            respawnTarget = transform;
    }

    [ProButton]
    public void PlayerRespawn() {
        if (SimpleTeleport.Instance == null) {
            Debug.LogWarning("[RespawnPoint] SimpleTeleport instance not found!");
            return;
        }

        if (respawnTarget == null) {
            Debug.LogWarning("[RespawnPoint] Respawn target is null!");
            return;
        }

        // Gán target cho SimpleTeleport
        SimpleTeleport.Instance.targetPosition = respawnTarget;

        // Gọi hàm teleport
        SimpleTeleport.Instance.Teleport();

        Debug.Log($"[RespawnPoint] Player respawned to {respawnTarget.position}");
    }
}
