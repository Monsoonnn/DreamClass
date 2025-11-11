using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Locomotion;

namespace DreamClass.Locomotion
{
    public class SimpleTeleport : SingletonCtrl<SimpleTeleport>
    {
        [Header("References")]
        [Tooltip("TeleportInteractor sẽ trigger event teleport")]
        [SerializeField] private TeleportInteractor teleportInteractor;

        [Tooltip("OVRCameraRig root")]
        [SerializeField] private OVRCameraRig cameraRig;

        [Tooltip("OVRPlayerController (nếu có)")]
        [SerializeField] private OVRPlayerController playerController;

        [Header("Teleport Settings")]
        [Tooltip("Giữ hướng nhìn ban đầu (ignore target rotation)")]
        [SerializeField] private bool keepOriginalRotation = true;

        [Tooltip("Align feet thay vì eye level")]
        [SerializeField] private bool alignFeet = true;

        [Header("Debug")]
        [Tooltip("Hiển thị debug logs")]
        [SerializeField] private bool showDebugLogs = true;

        private Transform playerRoot;
        private Transform centerEye;
        private OVRPlayerController characterController;
        private bool isSubscribed;
        private bool hasPlayerController;


        protected override void Start()
        {
            base.Start();
            InitializeReferences();
            SubscribeToTeleportEvents();
        }



        private void InitializeReferences()
        {
            // Tự động tìm references nếu chưa assign
            if (cameraRig == null)
            {
                cameraRig = FindObjectOfType<OVRCameraRig>();
            }

            if (teleportInteractor == null)
            {
                teleportInteractor = FindObjectOfType<TeleportInteractor>();
            }

            if (playerController == null)
            {
                playerController = FindObjectOfType<OVRPlayerController>();
            }

            // Determine player root hierarchy
            if (playerController != null)
            {
                // Nếu có OVRPlayerController, nó là root
                playerRoot = playerController.transform;
                hasPlayerController = true;
                characterController = playerController.GetComponent<OVRPlayerController>();
                
                if (showDebugLogs)
                    Debug.Log("[SimpleTeleport] Using OVRPlayerController as player root");
            }
            else if (cameraRig != null)
            {
                // Nếu không, dùng CameraRig
                playerRoot = cameraRig.transform;
                hasPlayerController = false;
                
                if (showDebugLogs)
                    Debug.Log("[SimpleTeleport] Using OVRCameraRig as player root");
            }

            if (cameraRig != null)
            {
                centerEye = cameraRig.centerEyeAnchor;
            }

            // Validate
            if (teleportInteractor == null)
            {
                Debug.LogError("[SimpleTeleport] TeleportInteractor not found!");
            }
            if (cameraRig == null)
            {
                Debug.LogError("[SimpleTeleport] OVRCameraRig not found!");
            }
            if (centerEye == null)
            {
                Debug.LogError("[SimpleTeleport] CenterEyeAnchor not found!");
            }
        }

        private void SubscribeToTeleportEvents()
        {
            if (teleportInteractor != null && !isSubscribed)
            {
                teleportInteractor.WhenLocomotionPerformed += HandleLocomotionEvent;
                isSubscribed = true;
                Debug.Log("[SimpleTeleport] Subscribed to TeleportInteractor events");
            }
        }

        private void UnsubscribeFromTeleportEvents()
        {
            if (teleportInteractor != null && isSubscribed)
            {
                teleportInteractor.WhenLocomotionPerformed -= HandleLocomotionEvent;
                isSubscribed = false;
            }
        }

        /// <summary>
        /// Xử lý LocomotionEvent từ TeleportInteractor
        /// </summary>
        private void HandleLocomotionEvent(LocomotionEvent locomotionEvent)
        {
            // Bỏ qua nếu không phải teleport
            if (locomotionEvent.Translation == LocomotionEvent.TranslationType.None)
            {
                Debug.Log("[SimpleTeleport] Teleport denied or blocked");
                return;
            }

            Pose targetPose = locomotionEvent.Pose;
            PerformCustomTeleport(targetPose, locomotionEvent);
        }

        /// <summary>
        /// Logic teleport với FIX offset XZ plane
        /// Support cả OVRPlayerController và OVRCameraRig
        /// </summary>
        private void PerformCustomTeleport(Pose targetPose, LocomotionEvent locomotionEvent)
        {
            if (playerRoot == null || centerEye == null)
            {
                Debug.LogWarning("[SimpleTeleport] Missing references!");
                return;
            }

            // CRITICAL: Set Teleported flag để OVRPlayerController không override rotation
            if (hasPlayerController && playerController != null)
            {
                playerController.Teleported = true;
            }

            // Disable CharacterController nếu có
            bool wasControllerEnabled = false;
            if (hasPlayerController && characterController != null)
            {
                wasControllerEnabled = characterController.enabled;
                characterController.enabled = false;
            }

            Vector3 targetPosition = targetPose.position;
            Quaternion targetRotation = targetPose.rotation;

            // --- TRANSLATION ---
            Vector3 finalPosition;

            if (locomotionEvent.Translation == LocomotionEvent.TranslationType.AbsoluteEyeLevel)
            {
                // Eye level mode: align head to target
                Vector3 headOffset = centerEye.position - playerRoot.position;
                finalPosition = targetPosition - headOffset;
            }
            else if (alignFeet)
            {
                // Feet mode: FIX - chỉ tính offset trên XZ plane
                Vector3 cameraWorldPos = centerEye.position;
                
                // Project camera position xuống mặt phẳng XZ (cùng Y với player root)
                Vector3 cameraFlatPos = new Vector3(cameraWorldPos.x, playerRoot.position.y, cameraWorldPos.z);
                
                // Tính offset chỉ trên XZ plane
                Vector3 offset = playerRoot.position - cameraFlatPos;
                
                finalPosition = targetPosition + offset;
                finalPosition.y = playerRoot.position.y;
            }
            else
            {
                finalPosition = targetPosition;
            }

            playerRoot.position = finalPosition;

            // --- ROTATION ---
            if (!keepOriginalRotation && locomotionEvent.Rotation == LocomotionEvent.RotationType.Absolute)
            {
                float currentYaw = centerEye.rotation.eulerAngles.y;
                float targetYaw = targetRotation.eulerAngles.y;
                float deltaYaw = targetYaw - currentYaw;

                // Chuẩn hóa góc về [-180, 180]
                while (deltaYaw > 180f) deltaYaw -= 360f;
                while (deltaYaw < -180f) deltaYaw += 360f;

                playerRoot.Rotate(0f, deltaYaw, 0f, Space.World);
            }

            // Re-enable CharacterController
            if (hasPlayerController && characterController != null)
            {
                characterController.enabled = wasControllerEnabled;
            }

            // NOTE: Teleported flag sẽ tự reset về false sau 1 frame trong OVRPlayerController.UpdateTransform()

            if (showDebugLogs)
                Debug.Log($"[SimpleTeleport] Teleported to {targetPosition}");
        }

        // ============================================
        // PUBLIC API (giống SimpleTeleport gốc)
        // ============================================

        /// <summary>
        /// Manual teleport (giống SimpleTeleport.Teleport)
        /// </summary>
        public void Teleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            ManualTeleport(worldPosition, worldRotation);
        }

        /// <summary>
        /// Manual teleport đến Transform (giống SimpleTeleport.TeleportToTarget)
        /// </summary>
        public void TeleportToTarget(Transform target)
        {
            if (target != null)
            {
                ManualTeleport(target.position, target.rotation);
            }
        }

        /// <summary>
        /// Set target và teleport ngay
        /// </summary>
        public void SetTargetAndTeleport(Transform target)
        {
            TeleportToTarget(target);
        }

        // ============================================
        // INTERNAL METHODS
        // ============================================

        /// <summary>
        /// Manual teleport implementation
        /// FIX: Tính offset đúng trên XZ plane để không bị ảnh hưởng khi xoay đầu
        /// Support cả OVRPlayerController và OVRCameraRig
        /// </summary>
        public void ManualTeleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (playerRoot == null || centerEye == null)
            {
                Debug.LogWarning("[SimpleTeleport] Missing references!");
                return;
            }

            // CRITICAL: Set Teleported flag để OVRPlayerController không override rotation
            if (hasPlayerController && playerController != null)
            {
                playerController.Teleported = true;
            }

            // Disable CharacterController nếu có (để tránh collision khi teleport)
            bool wasControllerEnabled = false;
            if (hasPlayerController && characterController != null)
            {
                wasControllerEnabled = characterController.enabled;
                characterController.enabled = false;
            }

            // Lấy vị trí camera hiện tại
            Vector3 cameraWorldPos = centerEye.position;
            
            // Project camera position xuống mặt phẳng XZ (cùng độ cao với player root)
            // Điều này đảm bảo offset không bị ảnh hưởng khi player ngước/cúi đầu
            Vector3 cameraFlatPos = new Vector3(cameraWorldPos.x, playerRoot.position.y, cameraWorldPos.z);
            
            // Tính offset chỉ trên XZ plane
            Vector3 offset = playerRoot.position - cameraFlatPos;

            // Vị trí mới = target + offset (để camera đứng đúng tại target)
            Vector3 newRootPos = worldPosition + offset;
            
            // Giữ nguyên Y của player root
            newRootPos.y = playerRoot.position.y;

            playerRoot.position = newRootPos;

            // Xoay nếu cần
            if (!keepOriginalRotation)
            {
                // Chỉ lấy yaw (góc xoay quanh trục Y)
                float currentYaw = centerEye.rotation.eulerAngles.y;
                float targetYaw = worldRotation.eulerAngles.y;
                float deltaYaw = targetYaw - currentYaw;

                // Chuẩn hóa góc về [-180, 180]
                while (deltaYaw > 180f) deltaYaw -= 360f;
                while (deltaYaw < -180f) deltaYaw += 360f;

                playerRoot.Rotate(0f, deltaYaw, 0f, Space.World);
            }

            // Re-enable CharacterController
            if (hasPlayerController && characterController != null)
            {
                characterController.enabled = wasControllerEnabled;
            }

            // NOTE: Teleported flag sẽ tự reset về false sau 1 frame trong OVRPlayerController.UpdateTransform()

            if (showDebugLogs)
                Debug.Log($"[SimpleTeleport] Manual teleport to {worldPosition}");
        }

        /// <summary>
        /// Manual teleport đến Transform
        /// </summary>
        public void ManualTeleportToTransform(Transform target)
        {
            if (target != null)
            {
                ManualTeleport(target.position, target.rotation);
            }
        }

        // ============================================
        // SETTERS
        // ============================================

        public void SetKeepOriginalRotation(bool value)
        {
            keepOriginalRotation = value;
        }

        public void SetAlignFeet(bool value)
        {
            alignFeet = value;
        }

        public bool GetKeepOriginalRotation()
        {
            return keepOriginalRotation;
        }

        public Transform GetPlayerRoot()
        {
            return playerRoot;
        }

        public Transform GetCenterEye()
        {
            return centerEye;
        }

        public OVRPlayerController GetPlayerController()
        {
            return playerController;
        }

        public bool HasPlayerController()
        {
            return hasPlayerController;
        }
    }
}