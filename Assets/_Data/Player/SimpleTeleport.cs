using System;
using System.Collections;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Locomotion;

namespace DreamClass.Locomotion
{
    /// <summary>
    /// SimpleTeleport - Ưu tiên OVRPlayerController, fallback FirstPersonLocomotor
    /// </summary>
    public class SimpleTeleport : SingletonCtrl<SimpleTeleport>
    {
        [Header("References")]
        [Tooltip("TeleportInteractor sẽ trigger event teleport")]
        [SerializeField] private TeleportInteractor teleportInteractor;

        [Tooltip("OVRCameraRig root")]
        [SerializeField] private OVRCameraRig cameraRig;

        [Tooltip("OVRPlayerController (ưu tiên) - optional")]
        [SerializeField] private OVRPlayerController playerController;

        [Tooltip("FirstPersonLocomotor (fallback) - optional")]
        [SerializeField] private FirstPersonLocomotor locomotor;

        [Header("Teleport Settings")]
        [Tooltip("Giữ hướng nhìn ban đầu (ignore target rotation)")]
        [SerializeField] private bool keepOriginalRotation = true;

        [Tooltip("Align feet thay vì eye level")]
        [SerializeField] private bool alignFeet = true;

        [Header("FirstPersonLocomotor Fix")]
        [Tooltip("Fix player origin sync issue khi dùng FirstPersonLocomotor")]
        [SerializeField] private bool fixFirstPersonLocomotor = true;

        [Tooltip("Delay (seconds) trước khi fix")]
        [SerializeField] private float fixDelay = 0.05f;

        [Header("Debug")]
        [Tooltip("Hiển thị debug logs")]
        [SerializeField] private bool showDebugLogs = true;

        private Transform playerRoot;
        private Transform centerEye;
        private Transform playerOrigin;
        private bool isSubscribed;


        protected override void Start()
        {
            base.Start();
            InitializeReferences();
            SubscribeToTeleportEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTeleportEvents();
        }

        private void InitializeReferences()
        {
            // Tự động tìm references nếu chưa assign
            if (cameraRig == null)
            {
                cameraRig = FindAnyObjectByType<OVRCameraRig>();
            }

            if (teleportInteractor == null)
            {
                teleportInteractor = FindAnyObjectByType<TeleportInteractor>();
            }

            if (playerController == null)
            {
                playerController = FindAnyObjectByType<OVRPlayerController>();
            }

            if (locomotor == null)
            {
                locomotor = FindAnyObjectByType<FirstPersonLocomotor>();
            }

            // Xác định locomotion system (ưu tiên OVRPlayerController)
            if (playerController != null)
            {
                playerRoot = playerController.transform;
                if (showDebugLogs)
                    Debug.Log("[SimpleTeleport] Using OVRPlayerController");
            }
            else if (cameraRig != null)
            {
                playerRoot = cameraRig.transform;
                if (showDebugLogs)
                    Debug.LogWarning("[SimpleTeleport] No locomotor found, using CameraRig transform");
            }

            if (cameraRig != null)
            {
                centerEye = cameraRig.centerEyeAnchor;
                playerOrigin = cameraRig.transform.parent != null ? cameraRig.transform.parent : cameraRig.transform;
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
            if (locomotionEvent.Translation == LocomotionEvent.TranslationType.None)
            {
                if (showDebugLogs)
                    Debug.Log("[SimpleTeleport] Teleport denied or blocked");
                return;
            }

            Pose targetPose = locomotionEvent.Pose;
            PerformTeleport(targetPose, locomotionEvent);
        }

        /// <summary>
        /// Perform teleport - ưu tiên OVRPlayerController
        /// </summary>
        private void PerformTeleport(Pose targetPose, LocomotionEvent locomotionEvent)
        {
            PerformOVRTeleport(targetPose, locomotionEvent);
            PerformLocomotorTeleport(targetPose, locomotionEvent);
        }

        #region OVRPlayerController Teleport

        /// <summary>
        /// Teleport sử dụng OVRPlayerController
        /// </summary>
        private void PerformOVRTeleport(Pose targetPose, LocomotionEvent locomotionEvent)
        {
            if (playerRoot == null || centerEye == null)
            {
                Debug.LogWarning("[SimpleTeleport] Missing references!");
                return;
            }

            // Set Teleported flag để OVRPlayerController không override rotation
            if (playerController != null)
            {
                playerController.Teleported = true;
            }

            // Disable CharacterController để teleport
            bool wasControllerEnabled = false;
            if (playerController != null)
            {
                wasControllerEnabled = playerController.enabled;
                playerController.enabled = false;
            }

            Vector3 targetPosition = targetPose.position;
            Quaternion targetRotation = targetPose.rotation;

            // --- TRANSLATION ---
            Vector3 finalPosition;

            if (locomotionEvent.Translation == LocomotionEvent.TranslationType.AbsoluteEyeLevel)
            {
                // Eye level mode
                Vector3 headOffset = centerEye.position - playerRoot.position;
                finalPosition = targetPosition - headOffset;
            }
            else if (alignFeet)
            {
                // Feet mode: tính offset trên XZ plane
                Vector3 cameraWorldPos = centerEye.position;
                Vector3 cameraFlatPos = new Vector3(cameraWorldPos.x, playerRoot.position.y, cameraWorldPos.z);
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

                while (deltaYaw > 180f) deltaYaw -= 360f;
                while (deltaYaw < -180f) deltaYaw += 360f;

                playerRoot.Rotate(0f, deltaYaw, 0f, Space.World);
            }

            // Re-enable CharacterController
            if (playerController != null)
            {
                playerController.enabled = wasControllerEnabled;
            }

            if (showDebugLogs)
                Debug.Log($"[SimpleTeleport] OVR teleport to {targetPosition}");
        }

        #endregion

        #region FirstPersonLocomotor Teleport

        /// <summary>
        /// Teleport sử dụng FirstPersonLocomotor + fix sync
        /// </summary>
        private void PerformLocomotorTeleport(Pose targetPose, LocomotionEvent originalEvent)
        {
            if (locomotor == null)
            {
                Debug.LogWarning("[SimpleTeleport] FirstPersonLocomotor not found!");
                return;
            }

            // Xác định translation type
            LocomotionEvent.TranslationType translationType = alignFeet
                ? LocomotionEvent.TranslationType.Absolute
                : LocomotionEvent.TranslationType.AbsoluteEyeLevel;

            // Xác định rotation type
            LocomotionEvent.RotationType rotationType = keepOriginalRotation
                ? LocomotionEvent.RotationType.None
                : LocomotionEvent.RotationType.Absolute;

            // Tạo LocomotionEvent
            LocomotionEvent customEvent = new LocomotionEvent(
                GetHashCode(),
                targetPose,
                translationType,
                rotationType
            );

            // Pass event đến FirstPersonLocomotor
            locomotor.HandleLocomotionEvent(customEvent);

            // Fix player origin sync
            if (fixFirstPersonLocomotor)
            {
                StartCoroutine(FixPlayerOriginSync(targetPose, translationType, rotationType));
            }

            if (showDebugLogs)
                Debug.Log($"[SimpleTeleport] FirstPersonLocomotor teleport to {targetPose.position}");
        }

        /// <summary>
        /// Fix player origin sync sau khi FirstPersonLocomotor xử lý
        /// </summary>
        private IEnumerator FixPlayerOriginSync(Pose targetPose, 
            LocomotionEvent.TranslationType translationType,
            LocomotionEvent.RotationType rotationType)
        {
            yield return new WaitForSeconds(fixDelay);

            if (playerOrigin == null || centerEye == null || playerRoot == null)
            {
                yield break;
            }

            Vector3 currentCameraPos = centerEye.position;
            Vector3 currentCameraFlatPos = new Vector3(currentCameraPos.x, playerOrigin.position.y, currentCameraPos.z);

            Vector3 newOriginPos = playerOrigin.position;

            if (translationType == LocomotionEvent.TranslationType.Absolute && alignFeet)
            {
                // Feet mode: Camera phải ở đúng target position
                Vector3 offset = currentCameraFlatPos - targetPose.position;
                newOriginPos = playerOrigin.position - offset;
                newOriginPos.y = playerOrigin.position.y;
            }
            else if (translationType == LocomotionEvent.TranslationType.AbsoluteEyeLevel)
            {
                // Eye level mode
                Vector3 headOffset = centerEye.position - playerOrigin.position;
                newOriginPos = targetPose.position - headOffset;
            }

            playerOrigin.position = newOriginPos;

            // Fix rotation
            if (rotationType == LocomotionEvent.RotationType.Absolute && !keepOriginalRotation)
            {
                float currentYaw = centerEye.rotation.eulerAngles.y;
                float targetYaw = targetPose.rotation.eulerAngles.y;
                float deltaYaw = targetYaw - currentYaw;

                while (deltaYaw > 180f) deltaYaw -= 360f;
                while (deltaYaw < -180f) deltaYaw += 360f;

                playerOrigin.Rotate(0f, deltaYaw, 0f, Space.World);
            }

            if (showDebugLogs)
                Debug.Log($"[SimpleTeleport] Fixed player origin sync");
        }

        #endregion

        // ============================================
        // PUBLIC API
        // ============================================

        public void Teleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            ManualTeleport(worldPosition, worldRotation);
        }

        public void TeleportToTarget(Transform target)
        {
            if (target != null)
            {
                ManualTeleport(target.position, target.rotation);
            }
        }

        public void SetTargetAndTeleport(Transform target)
        {
            TeleportToTarget(target);
        }

        public void ManualTeleport(Vector3 worldPosition, Quaternion worldRotation)
        {
            Pose targetPose = new Pose(worldPosition, worldRotation);
            ManualOVRTeleport(targetPose);
            ManualLocomotorTeleport(targetPose);

        }

        public void ManualTeleportToTransform(Transform target)
        {
            if (target != null)
            {
                ManualTeleport(target.position, target.rotation);
            }
        }

        #region Manual Teleport Implementations

        private void ManualOVRTeleport(Pose targetPose)
        {
            if (playerRoot == null || centerEye == null)
            {
                Debug.LogWarning("[SimpleTeleport] Missing references!");
                return;
            }

            if (playerController != null)
            {
                playerController.Teleported = true;
            }

            bool wasControllerEnabled = false;
            if (playerController != null)
            {
                wasControllerEnabled = playerController.enabled;
                playerController.enabled = false;
            }

            Vector3 cameraWorldPos = centerEye.position;
            Vector3 cameraFlatPos = new Vector3(cameraWorldPos.x, playerRoot.position.y, cameraWorldPos.z);
            Vector3 offset = playerRoot.position - cameraFlatPos;

            Vector3 newRootPos = targetPose.position + offset;
            newRootPos.y = playerRoot.position.y;

            playerRoot.position = newRootPos;

            if (!keepOriginalRotation)
            {
                float currentYaw = centerEye.rotation.eulerAngles.y;
                float targetYaw = targetPose.rotation.eulerAngles.y;
                float deltaYaw = targetYaw - currentYaw;

                while (deltaYaw > 180f) deltaYaw -= 360f;
                while (deltaYaw < -180f) deltaYaw += 360f;

                playerRoot.Rotate(0f, deltaYaw, 0f, Space.World);
            }

            if (playerController != null)
            {
                playerController.enabled = wasControllerEnabled;
            }

            if (showDebugLogs)
                Debug.Log($"[SimpleTeleport] Manual OVR teleport to {targetPose.position}");
        }

        private void ManualLocomotorTeleport(Pose targetPose)
        {
            if (locomotor == null)
            {
                Debug.LogWarning("[SimpleTeleport] FirstPersonLocomotor not found!");
                return;
            }

            LocomotionEvent.TranslationType translationType = alignFeet
                ? LocomotionEvent.TranslationType.Absolute
                : LocomotionEvent.TranslationType.AbsoluteEyeLevel;

            LocomotionEvent.RotationType rotationType = keepOriginalRotation
                ? LocomotionEvent.RotationType.None
                : LocomotionEvent.RotationType.Absolute;

            LocomotionEvent locomotionEvent = new LocomotionEvent(
                GetHashCode(),
                targetPose,
                translationType,
                rotationType
            );

            locomotor.HandleLocomotionEvent(locomotionEvent);

            if (fixFirstPersonLocomotor)
            {
                StartCoroutine(FixPlayerOriginSync(targetPose, translationType, rotationType));
            }

            if (showDebugLogs)
                Debug.Log($"[SimpleTeleport] Manual FirstPersonLocomotor teleport to {targetPose.position}");
        }

        #endregion

        // ============================================
        // SETTERS & GETTERS
        // ============================================

        public void SetKeepOriginalRotation(bool value)
        {
            keepOriginalRotation = value;
        }

        public void SetAlignFeet(bool value)
        {
            alignFeet = value;
        }

        public void SetFixFirstPersonLocomotor(bool value)
        {
            fixFirstPersonLocomotor = value;
        }

        public void SetFixDelay(float delay)
        {
            fixDelay = Mathf.Max(0f, delay);
        }

        public bool GetKeepOriginalRotation()
        {
            return keepOriginalRotation;
        }

        public bool GetAlignFeet()
        {
            return alignFeet;
        }

        public Transform GetPlayerRoot()
        {
            return playerRoot;
        }

        public Transform GetCenterEye()
        {
            return centerEye;
        }

        public FirstPersonLocomotor GetLocomotor()
        {
            return locomotor;
        }

        public OVRPlayerController GetPlayerController()
        {
            return playerController;
        }
    }
}