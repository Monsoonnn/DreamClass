// using UnityEngine;
// using System.Collections;
// using Oculus.Interaction.Input;

// public class VRBookInteraction : MonoBehaviour {
//     [Header("References")]
//     public BookController bookController;

//     [Header("VR Controller Settings")]
//     public OVRInput.Controller vrController = OVRInput.Controller.RTouch;
//     public LayerMask bookLayer;
//     public float rayDistance = 10f;
//     public bool showDebugRay = true;

//     [Header("Hand Tracking")]
//     public bool enableHandTracking = true;
//     public OVRHand rightHand;
//     public OVRHand leftHand;
//     public OVRSkeleton rightHandSkeleton;
//     public OVRSkeleton leftHandSkeleton;
//     public float handRaycastDistance = 0.5f;

//     [Header("Interaction Thresholds")]
//     [Tooltip("Ngưỡng pinch strength để coi như đang pinch (so sánh với giá trị OVR trả về 0-1)")]
//     [Range(0f, 1f)]
//     public float pinchThreshold = 0.7f;
    
//     [Tooltip("Ngưỡng curl strength trung bình để coi như đang grab (so sánh với giá trị OVR trả về 0-1)")]
//     [Range(0f, 1f)]
//     public float grabThreshold = 0.15f;

//     [Header("Poke Interaction")]
//     public bool enablePokeInteraction = true;
//     [Tooltip("Khoảng cách ngón tay đến book plane để kích hoạt poke (meters)")]
//     public float pokeDepthThreshold = 0.02f;

//     // Private VR variables
//     private OVRCameraRig cameraRig;
//     private Transform controllerAnchor;
//     private bool isGrippingRightPage = false;
//     private bool isGrippingLeftPage = false;
//     private LineRenderer debugLineRenderer;
//     private LineRenderer handDebugLineRenderer;

//     // Hand tracking state
//     public const OVRSkeleton.BoneId INDEX_TIP_BONE = OVRSkeleton.BoneId.Hand_IndexTip;
//     private bool isHandTrackingActive = false;
//     private bool isPinchGrabbing = false;
//     private Vector3 lastHandHitPoint;
//     private Plane bookPlane;
//     private Vector3 currentTargetPoint;

//     void Start() {
//         if (bookController == null) {
//             bookController = GetComponent<BookController>();
//             if (bookController == null) {
//                 Debug.LogError("BookController not found!");
//                 return;
//             }
//         }

//         StartCoroutine(DelayedVRInitialization());

//         if (showDebugRay) {
//             SetupDebugLineRenderer();
//             SetupHandDebugLineRenderer();
//         }

//         UpdateBookPlane();
//     }

//     IEnumerator DelayedVRInitialization() {
//         yield return new WaitForEndOfFrame();
//         yield return new WaitForSeconds(0.5f);
//         InitializeVR();
//     }

//     void InitializeVR() {
//         cameraRig = FindObjectOfType<OVRCameraRig>();
//         if (cameraRig != null) {
//             UpdateControllerAnchor();
//             if (enableHandTracking) InitializeHandTracking();
//         } else {
//             Debug.LogError("OVRCameraRig not found!");
//         }
//     }

//     void InitializeHandTracking() {
//         var sources = GameObject.FindObjectsOfType<FromOVRHandDataSource>();

//         if (rightHand == null && sources.Length > 1)
//             rightHand = sources[1].GetComponent<OVRHand>();
//         if (leftHand == null && sources.Length > 0)
//             leftHand = sources[0].GetComponent<OVRHand>();

//         var skeletons = GameObject.FindObjectsOfType<OVRSkeleton>();

//         if (rightHandSkeleton == null && rightHand != null && skeletons.Length > 0)
//             rightHandSkeleton = skeletons[0];
//         if (leftHandSkeleton == null && leftHand != null && skeletons.Length > 1)
//             leftHandSkeleton = skeletons[1];

//         Debug.Log(rightHand != null || leftHand != null ? "✓ Hand tracking initialized" : "Hand tracking not found");
//     }

//     void UpdateControllerAnchor() {
//         if (cameraRig == null) return;
//         controllerAnchor = (vrController == OVRInput.Controller.RTouch) ? cameraRig.rightHandAnchor : cameraRig.leftHandAnchor;
//     }

//     void SetupDebugLineRenderer() {
//         GameObject lineObj = new GameObject("ControllerDebugRay");
//         lineObj.transform.SetParent(transform);
//         debugLineRenderer = lineObj.AddComponent<LineRenderer>();
//         debugLineRenderer.startWidth = 0.005f;
//         debugLineRenderer.endWidth = 0.005f;
//         debugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//         debugLineRenderer.startColor = Color.red;
//         debugLineRenderer.endColor = Color.yellow;
//         debugLineRenderer.positionCount = 2;
//     }

//     void SetupHandDebugLineRenderer() {
//         GameObject lineObj = new GameObject("HandDebugRay");
//         lineObj.transform.SetParent(transform);
//         handDebugLineRenderer = lineObj.AddComponent<LineRenderer>();
//         handDebugLineRenderer.startWidth = 0.003f;
//         handDebugLineRenderer.endWidth = 0.003f;
//         handDebugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//         handDebugLineRenderer.startColor = Color.cyan;
//         handDebugLineRenderer.endColor = Color.blue;
//         handDebugLineRenderer.positionCount = 2;
//     }

//     void UpdateBookPlane() {
//         if (bookController == null) return;
//         RectTransform bookPanel = bookController.GetBookPanel();
//         if (bookPanel != null) {
//             bookPlane = new Plane(bookPanel.transform.forward, bookPanel.transform.position);
//         }
//     }

//     void Update() {
//         if (bookController == null || !bookController.interactable) return;

//         UpdateBookPlane();
//         isHandTrackingActive = IsHandTrackingActive();

//         if (isHandTrackingActive && enableHandTracking) {
//             HandleHandTrackingInput();
//         } else {
//             HandleControllerInput();
//         }

//         if (bookController.IsPageDragging) {
//             bookController.UpdateBook(currentTargetPoint);
//         }

//         UpdateDebugRays();
//     }

//     bool IsHandTrackingActive() {
//         if (rightHand != null && rightHand.IsTracked && rightHand.HandConfidence == OVRHand.TrackingConfidence.High)
//             return true;
//         if (leftHand != null && leftHand.IsTracked && leftHand.HandConfidence == OVRHand.TrackingConfidence.High)
//             return true;
//         return false;
//     }

//     Transform GetFingerTip(OVRHand hand) {
//         if (hand == null) return null;

//         OVRSkeleton skeleton = (hand == rightHand) ? rightHandSkeleton : leftHandSkeleton;
//         if (skeleton == null || skeleton.Bones == null || skeleton.Bones.Count == 0) return null;

//         foreach (var bone in skeleton.Bones) {
//             if (bone.Id == INDEX_TIP_BONE) {
//                 return bone.Transform;
//             }
//         }
//         return null;
//     }

//     void HandleHandTrackingInput() {
//         OVRHand activeHand = GetActiveHand();
//         if (activeHand == null) return;

//         Transform fingerTip = GetFingerTip(activeHand);
//         if (fingerTip == null) return;

//         // ĐỌC giá trị pinch strength TRỰC TIẾP từ OVR (0-1)
//         float pinchStrength = activeHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        
//         // So sánh với threshold
//         bool isPinching = pinchStrength > pinchThreshold;
//         bool isGrabbing = IsHandGrabbing(activeHand);

//         // Raycast từ ngón tay
//         Ray fingerRay = new Ray(fingerTip.position, fingerTip.forward);
//         RaycastHit hit;

//         if (Physics.Raycast(fingerRay, out hit, handRaycastDistance, bookLayer)) {
//             if ((isPinching || isGrabbing) && !bookController.IsPageDragging) {
//                 OnHandGrabPage(hit.point, activeHand);
//             } else if (bookController.IsPageDragging) {
//                 currentTargetPoint = bookController.TransformPoint(bookPlane.ClosestPointOnPlane(fingerTip.position));
//             }
//         }

//         if (enablePokeInteraction) {
//             HandlePokeInteraction(fingerTip, activeHand, isPinching || isGrabbing);
//         }

//         if (!isPinching && !isGrabbing && bookController.IsPageDragging && isPinchGrabbing) {
//             OnHandRelease();
//         }
//     }

//     bool IsHandGrabbing(OVRHand hand) {
//         if (hand == null) return false;

//         // ĐỌC TRỰC TIẾP giá trị curl/pinch strength từ OVR (0-1)
//         // KHÔNG thay đổi, KHÔNG normalize
//         float middleCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
//         float ringCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
//         float pinkyCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);
//         float thumbCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb);
//         float indexCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

//         // Tính trung bình (chỉ để so sánh)
//         float avgCurl = (middleCurl + ringCurl + pinkyCurl + thumbCurl + indexCurl) / 5f;

//         // So sánh với threshold
//         return avgCurl > grabThreshold;
//     }

//     void HandlePokeInteraction(Transform fingerTip, OVRHand hand, bool isPinching) {
//         float distanceToPlane = bookPlane.GetDistanceToPoint(fingerTip.position);

//         if (Mathf.Abs(distanceToPlane) < pokeDepthThreshold) {
//             Vector3 projectedPoint = bookPlane.ClosestPointOnPlane(fingerTip.position);
//             Vector3 localPoint = bookController.GetBookPanel().InverseTransformPoint(projectedPoint);

//             if (IsPointInBookBounds(localPoint)) {
//                 if (isPinching && !bookController.IsPageDragging) {
//                     OnHandGrabPage(projectedPoint, hand);
//                 }
//             }
//         }
//     }

//     bool IsPointInBookBounds(Vector3 localPoint) {
//         RectTransform bookPanel = bookController.GetBookPanel();
//         float halfWidth = bookPanel.rect.width / 2;
//         float halfHeight = bookPanel.rect.height / 2;
//         return Mathf.Abs(localPoint.x) <= halfWidth && Mathf.Abs(localPoint.y) <= halfHeight;
//     }

//     OVRHand GetActiveHand() {
//         if (rightHand != null && rightHand.IsTracked && rightHand.HandConfidence == OVRHand.TrackingConfidence.High)
//             return rightHand;
//         if (leftHand != null && leftHand.IsTracked && leftHand.HandConfidence == OVRHand.TrackingConfidence.High)
//             return leftHand;
//         return null;
//     }

//     void OnHandGrabPage(Vector3 worldPoint, OVRHand hand) {
//         Vector3 localPoint = bookController.GetBookPanel().InverseTransformPoint(worldPoint);

//         if (localPoint.x > 0) {
//             if (bookController.currentPage >= bookController.TotalPageCount) return;
//             isGrippingRightPage = true;
//             isPinchGrabbing = true;
//             Vector3 p = bookController.TransformPoint(worldPoint);
//             bookController.DragRightPageToPoint(p);
//             currentTargetPoint = p;
//         } else {
//             if (bookController.currentPage <= 0) return;
//             isGrippingLeftPage = true;
//             isPinchGrabbing = true;
//             Vector3 p = bookController.TransformPoint(worldPoint);
//             bookController.DragLeftPageToPoint(p);
//             currentTargetPoint = p;
//         }

//         lastHandHitPoint = worldPoint;
//         Debug.Log($"✓ Hand grabbed {(localPoint.x > 0 ? "right" : "left")} page");
//     }

//     void OnHandRelease() {
//         isPinchGrabbing = false;
//         isGrippingRightPage = false;
//         isGrippingLeftPage = false;
//         bookController.ReleasePage();
//         Debug.Log("✓ Hand released page");
//     }

//     void HandleControllerInput() {
//         if (controllerAnchor == null || !IsControllerValid()) return;

//         // ĐỌC TRỰC TIẾP input state từ OVR controller
//         bool triggerPressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, vrController);
//         bool triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, vrController);
//         bool triggerUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, vrController);

//         Ray ray = GetControllerRay();
//         RaycastHit hit;

//         if (Physics.Raycast(ray, out hit, rayDistance, bookLayer)) {
//             if (triggerDown && !bookController.IsPageDragging) {
//                 Vector3 localPoint = bookController.GetBookPanel().InverseTransformPoint(hit.point);
//                 Vector3 p = bookController.TransformPoint(hit.point);
                
//                 if (localPoint.x > 0) {
//                     isGrippingRightPage = true;
//                     bookController.DragRightPageToPoint(p);
//                 } else {
//                     isGrippingLeftPage = true;
//                     bookController.DragLeftPageToPoint(p);
//                 }
//                 currentTargetPoint = p;
//             } else if (triggerPressed && bookController.IsPageDragging) {
//                 currentTargetPoint = bookController.TransformPoint(hit.point);
//             }
//         }

//         if (triggerUp && bookController.IsPageDragging) {
//             OnVRRelease();
//         }
//     }

//     bool IsControllerValid() {
//         // ĐỌC vị trí controller, kiểm tra valid
//         return !float.IsNaN(controllerAnchor.position.x) &&
//                !float.IsNaN(controllerAnchor.position.y) &&
//                !float.IsNaN(controllerAnchor.position.z);
//     }

//     Ray GetControllerRay() {
//         // ĐỌC vị trí và hướng controller
//         if (controllerAnchor != null && IsControllerValid()) {
//             return new Ray(controllerAnchor.position, controllerAnchor.forward);
//         }
//         if (Camera.main != null) {
//             return new Ray(Camera.main.transform.position, Camera.main.transform.forward);
//         }
//         return new Ray(Vector3.zero, Vector3.forward);
//     }

//     void UpdateDebugRays() {
//         if (!showDebugRay) return;

//         if (debugLineRenderer != null && controllerAnchor != null && IsControllerValid()) {
//             Ray ray = GetControllerRay();
//             debugLineRenderer.SetPosition(0, ray.origin);
//             debugLineRenderer.SetPosition(1, ray.origin + ray.direction * rayDistance);
//             debugLineRenderer.enabled = !isHandTrackingActive;
//         }

//         if (handDebugLineRenderer != null && isHandTrackingActive) {
//             OVRHand activeHand = GetActiveHand();
//             Transform fingerTip = GetFingerTip(activeHand);

//             if (fingerTip != null) {
//                 handDebugLineRenderer.SetPosition(0, fingerTip.position);
//                 handDebugLineRenderer.SetPosition(1, fingerTip.position + fingerTip.forward * handRaycastDistance);
//                 handDebugLineRenderer.enabled = true;
//             } else {
//                 handDebugLineRenderer.enabled = false;
//             }
//         } else if (handDebugLineRenderer != null) {
//             handDebugLineRenderer.enabled = false;
//         }
//     }

//     void OnVRRelease() {
//         isGrippingRightPage = false;
//         isGrippingLeftPage = false;
//         bookController.ReleasePage();
//     }

//     void OnDrawGizmos() {
//         if (!showDebugRay || bookController == null) return;

//         if (controllerAnchor != null && IsControllerValid() && !isHandTrackingActive) {
//             Gizmos.color = Color.red;
//             Gizmos.DrawRay(controllerAnchor.position, controllerAnchor.forward * rayDistance);
//             Gizmos.DrawWireSphere(controllerAnchor.position, 0.02f);
//         }

//         if (isHandTrackingActive) {
//             OVRHand activeHand = GetActiveHand();
//             Transform fingerTip = GetFingerTip(activeHand);

//             if (fingerTip != null) {
//                 Gizmos.color = Color.cyan;
//                 Gizmos.DrawRay(fingerTip.position, fingerTip.forward * handRaycastDistance);
//                 Gizmos.DrawWireSphere(fingerTip.position, 0.01f);

//                 if (enablePokeInteraction) {
//                     Gizmos.color = Color.green;
//                     Gizmos.DrawWireSphere(fingerTip.position, pokeDepthThreshold);
//                 }
//             }
//         }

//         RectTransform bookPanel = bookController.GetBookPanel();
//         if (bookPanel != null) {
//             Gizmos.color = Color.yellow;
//             Gizmos.matrix = bookPanel.transform.localToWorldMatrix;
//             Gizmos.DrawWireCube(Vector3.zero, new Vector3(bookPanel.rect.width, bookPanel.rect.height, 0.01f));
//         }
//     }
// }