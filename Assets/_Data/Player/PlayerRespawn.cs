using UnityEngine;
#if UNITY_EDITOR
using com.cyborgAssets.inspectorButtonPro;
#endif

namespace DreamClass.Locomotion
{
    /// <summary>
    /// Respawn point tương thích với Meta XR CustomTeleportHandler
    /// </summary>
    public class RespawnPoint : MonoBehaviour
    {
        [Header("Respawn Settings")]
        [Tooltip("Điểm đích teleport đến khi respawn")]
        [SerializeField] private Transform respawnTarget;

        [Tooltip("Giữ hướng nhìn hiện tại thay vì xoay theo target")]
        [SerializeField] private bool keepPlayerRotation = false;


        protected void Reset()
        {
            // Default target là chính object này
            if (respawnTarget == null)
                respawnTarget = transform;
        }


#if UNITY_EDITOR
        [ProButton]
#endif
        public void PlayerRespawn()
        {
            // Sử dụng Singleton nếu reference chưa set
            var handler = SimpleTeleport.Instance;

            if (handler == null)
            {
                Debug.LogError("[RespawnPoint] CustomTeleportHandler not found!");
                return;
            }

            if (respawnTarget == null)
            {
                Debug.LogError("[RespawnPoint] Respawn target is null!");
                return;
            }

            // Backup rotation setting
            bool originalKeepRotation = handler.GetKeepOriginalRotation();

            // Set rotation cho respawn này
            if (keepPlayerRotation)
            {
                handler.SetKeepOriginalRotation(true);
            }

            // Thực hiện teleport
            handler.ManualTeleportToTransform(respawnTarget);

            // Restore setting cũ
            handler.SetKeepOriginalRotation(originalKeepRotation);

            Debug.Log($"[RespawnPoint] Player respawned to {respawnTarget.position}");
        }

        /// <summary>
        /// Respawn với tùy chỉnh rotation
        /// </summary>
        public void PlayerRespawnWithRotation(bool keepRotation)
        {
            if (!ValidateReferences())
                return;

            SimpleTeleport.Instance.SetKeepOriginalRotation(keepRotation);
            SimpleTeleport.Instance.ManualTeleportToTransform(respawnTarget);

            Debug.Log($"[RespawnPoint] Player respawned (keepRotation: {keepRotation})");
        }

        /// <summary>
        /// Respawn đến vị trí cụ thể
        /// </summary>
        public void RespawnToPosition(Vector3 position, Quaternion rotation)
        {
            if (!ValidateReferences())
                return;

            SimpleTeleport.Instance.ManualTeleport(position, rotation);
            Debug.Log($"[RespawnPoint] Player respawned to custom position: {position}");
        }

        private bool ValidateReferences()
        {
            if (SimpleTeleport.Instance == null)
            {
                Debug.LogError("[RespawnPoint] CustomTeleportHandler not assigned!");
                return false;
            }

            if (respawnTarget == null)
            {
                Debug.LogError("[RespawnPoint] Respawn target is null!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Set target mới cho respawn
        /// </summary>
        public void SetRespawnTarget(Transform newTarget)
        {
            respawnTarget = newTarget;
        }

#if UNITY_EDITOR
        // Debug visualization
        private void OnDrawGizmos()
        {
            if (respawnTarget == null)
                return;

            // Vẽ vị trí respawn
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(respawnTarget.position, 0.5f);
            
            // Vẽ hướng nhìn
            Gizmos.color = Color.blue;
            Vector3 forward = respawnTarget.forward * 1f;
            Gizmos.DrawRay(respawnTarget.position, forward);
            
            // Vẽ label
            UnityEditor.Handles.Label(
                respawnTarget.position + Vector3.up * 0.7f, 
                "RESPAWN",
                new GUIStyle() { 
                    normal = new GUIStyleState() { textColor = Color.green },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                }
            );
        }

        private void OnDrawGizmosSelected()
        {
            if (respawnTarget == null)
                return;

            // Vẽ connection line từ script đến target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, respawnTarget.position);
        }
#endif
    }
}