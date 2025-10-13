using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

public class PlayerRespawn : MonoBehaviour {
    [SerializeField] private Transform centerEyeAnchor;
    [SerializeField] private OVRManager ovrManager;
    [SerializeField] private string spawnPositionName = "SpawnPosition";
    [SerializeField] private bool rotateToMatchSpawn = true;

    [ProButton]
    public void Respawn() {
        var spawn = FindSpawnPosition();
        if (spawn == null) {
            Debug.LogError($"Spawn point \"{spawnPositionName}\" not found!");
            return;
        }

        if (centerEyeAnchor == null) {
            Debug.LogError("centerEyeAnchor is not assigned!");
            return;
        }

        bool prevPosTracking = ovrManager.usePositionTracking;
        bool prevRotTracking = ovrManager.useRotationTracking;
        ovrManager.usePositionTracking = false;
        ovrManager.useRotationTracking = false;

        Vector3 originOffset = transform.localPosition - centerEyeAnchor.localPosition;

        if (rotateToMatchSpawn) {
            Vector3 currentForward = new Vector3(centerEyeAnchor.forward.x, 0, centerEyeAnchor.forward.z).normalized;
            Vector3 targetForward = new Vector3(spawn.transform.forward.x, 0, spawn.transform.forward.z).normalized;
            float angle = Vector3.SignedAngle(currentForward, targetForward, Vector3.up);
            transform.RotateAround(centerEyeAnchor.position, Vector3.up, angle);
        }

        OVRManager.display.RecenterPose();

        ovrManager.usePositionTracking = prevPosTracking;
        ovrManager.useRotationTracking = prevRotTracking;

        Debug.Log($"Player respawned at: {spawn.transform.position}");
    }



    private SpawnPosition FindSpawnPosition() {
        var allSpawns = FindObjectsOfType<SpawnPosition>(true);
        foreach (var spawn in allSpawns) {
            if (spawn.name == spawnPositionName)
                return spawn;
        }

        return allSpawns.Length > 0 ? allSpawns[0] : null;
    }
}