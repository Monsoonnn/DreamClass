// using UnityEngine;
// using System.Collections;
// using UnityEngine.UI;
// using UnityEngine.Events;
// using Oculus.Interaction.Input;

// public enum FlipMode {
//     RightToLeft,
//     LeftToRight
// }

// [ExecuteInEditMode]
// public class BookVR : MonoBehaviour {
//     [Header("VR Settings")]
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
//     public float grabThreshold = 0.15f;

//     [Header("Poke Interaction")]
//     public bool enablePokeInteraction = true;
//     public float pokeDepthThreshold = 0.02f;
//     public float pinchThresholdToGrab = 0.7f;

//     [Header("Book Settings")]
//     public Canvas canvas;
//     [SerializeField] RectTransform BookPanel;
//     public Sprite background;
//     public Sprite[] bookPages;
//     public bool interactable = true;
//     public bool enableShadowEffect = true;

//     public int currentPage = 0;
//     public int TotalPageCount { get { return bookPages.Length; } }
//     public Vector3 EndBottomLeft { get { return ebl; } }
//     public Vector3 EndBottomRight { get { return ebr; } }
//     public float Height { get { return BookPanel.rect.height; } }

//     public Image ClippingPlane;
//     public Image NextPageClip;
//     public Image Shadow;
//     public Image ShadowLTR;
//     public Image Left;
//     public Image LeftNext;
//     public Image Right;
//     public Image RightNext;
//     public UnityEvent OnFlip;

//     float radius1, radius2;
//     Vector3 sb, st, c, ebr, ebl, f;
//     bool pageDragging = false;
//     FlipMode mode;

//     // VR specific
//     private OVRCameraRig cameraRig;
//     private Transform controllerAnchor;
//     private bool isGrippingRightPage = false;
//     private bool isGrippingLeftPage = false;
//     private LineRenderer debugLineRenderer;
//     private LineRenderer handDebugLineRenderer;

//     // Hand tracking - FIXED: Store bone IDs instead of Transform references
//     public const OVRSkeleton.BoneId INDEX_TIP_BONE = OVRSkeleton.BoneId.Hand_IndexTip;
//     private bool isHandTrackingActive = false;
//     private bool isPinchGrabbing = false;
//     private Vector3 lastHandHitPoint;
//     private Plane bookPlane;

//     void Start() {
//         if (!canvas) canvas = GetComponentInParent<Canvas>();
//         if (!canvas) Debug.LogError("Book should be a child to canvas");

//         Left.gameObject.SetActive(false);
//         Right.gameObject.SetActive(false);
//         UpdateSprites();
//         CalcCurlCriticalPoints();

//         StartCoroutine(DelayedVRInitialization());

//         float pageWidth = BookPanel.rect.width / 2.0f;
//         float pageHeight = BookPanel.rect.height;
//         NextPageClip.rectTransform.sizeDelta = new Vector2(pageWidth, pageHeight + pageHeight * 2);
//         ClippingPlane.rectTransform.sizeDelta = new Vector2(pageWidth * 2 + pageHeight, pageHeight + pageHeight * 2);

//         float hyp = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
//         float shadowPageHeight = pageWidth / 2 + hyp;

//         Shadow.rectTransform.sizeDelta = new Vector2(pageWidth, shadowPageHeight);
//         Shadow.rectTransform.pivot = new Vector2(1, (pageWidth / 2) / shadowPageHeight);

//         ShadowLTR.rectTransform.sizeDelta = new Vector2(pageWidth, shadowPageHeight);
//         ShadowLTR.rectTransform.pivot = new Vector2(0, (pageWidth / 2) / shadowPageHeight);

//         if (showDebugRay) {
//             SetupDebugLineRenderer();
//             SetupHandDebugLineRenderer();
//         }

//         bookPlane = new Plane(BookPanel.transform.forward, BookPanel.transform.position);
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

//         var scoure = GameObject.FindObjectsOfType<FromOVRHandDataSource>();

//         if (rightHand == null)
//             rightHand = scoure[1].GetComponent<OVRHand>();
//         if (leftHand == null)
//             leftHand = scoure[0].GetComponent<OVRHand>();

//        var scoureSkeleton = GameObject.FindObjectsOfType<OVRSkeleton>();

//         if (rightHandSkeleton == null && rightHand != null)
//             rightHandSkeleton = scoureSkeleton[0];
//         if (leftHandSkeleton == null && leftHand != null)
//             leftHandSkeleton = scoureSkeleton[1];

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

//     void CalcCurlCriticalPoints() {
//         sb = new Vector3(0, -BookPanel.rect.height / 2);
//         ebr = new Vector3(BookPanel.rect.width / 2, -BookPanel.rect.height / 2);
//         ebl = new Vector3(-BookPanel.rect.width / 2, -BookPanel.rect.height / 2);
//         st = new Vector3(0, BookPanel.rect.height / 2);
//         radius1 = Vector2.Distance(sb, ebr);
//         float pageWidth = BookPanel.rect.width / 2.0f;
//         float pageHeight = BookPanel.rect.height;
//         radius2 = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
//     }

//     void Update() {
//         if (!interactable) return;

//         bookPlane.SetNormalAndPosition(BookPanel.transform.forward, BookPanel.transform.position);
//         isHandTrackingActive = IsHandTrackingActive();

//         if (isHandTrackingActive && enableHandTracking) {
//             HandleHandTrackingInput();
//         } else {
//             HandleControllerInput();
//         }

//         if (pageDragging) {
//             UpdateBook();
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

//     // FIXED: Get bone transform safely each time
//     Transform GetFingerTip( OVRHand hand ) {
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

//     Transform GetFingerTipAlt( OVRSkeleton skeleton ) {
//         if (skeleton == null) return null;

//         var bones = skeleton.Bones;
//         if (bones == null || bones.Count == 0) return null;

//         foreach (var bone in bones) {
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

//         // Kiểm tra cả pinch và grab
//         float pinchStrength = activeHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
//         bool isPinching = pinchStrength > pinchThresholdToGrab;
//         bool isGrabbing = IsHandGrabbing(activeHand);  

//         // Raycast từ ngón tay
//         Ray fingerRay = new Ray(fingerTip.position, fingerTip.forward);
//         RaycastHit hit;

//         if (Physics.Raycast(fingerRay, out hit, handRaycastDistance, bookLayer)) {
//             // Chấp nhận cả pinch hoặc grab
//             if ((isPinching || isGrabbing) && !pageDragging) {  
//                 OnHandGrabPage(hit.point, activeHand);
//             }
//         }

//         // Poke detection
//         if (enablePokeInteraction) {
//             HandlePokeInteraction(fingerTip, activeHand, isPinching || isGrabbing);  
//         }

//         // Release khi cả pinch và grab đều thả
//         if (!isPinching && !isGrabbing && pageDragging && isPinchGrabbing) {  
//             OnHandRelease();
//         }
//     }

//     bool IsHandGrabbing( OVRHand hand ) {
//         if (hand == null) return false;

//         float middleCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
//         float ringCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
//         float pinkyCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);
//         float thumbCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb);
//         float indexCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

//         // Grab = tất cả ngón đều curl mạnh
//         /*float avgCurl = (middleCurl + ringCurl + pinkyCurl) / 3f;*/

//         float avgCurl = (middleCurl + ringCurl + pinkyCurl + thumbCurl + indexCurl) / 5f;

//         //Debug.Log("IsHandGrabbing: " + (avgCurl > grabThreshold) + "AVG: " + avgCurl);

//         return avgCurl > grabThreshold;
//     }



//     void HandlePokeInteraction( Transform fingerTip, OVRHand hand, bool isPinching ) {
//         float distanceToPlane = bookPlane.GetDistanceToPoint(fingerTip.position);

//         if (Mathf.Abs(distanceToPlane) < pokeDepthThreshold) {
//             Vector3 projectedPoint = bookPlane.ClosestPointOnPlane(fingerTip.position);
//             Vector3 localPoint = BookPanel.InverseTransformPoint(projectedPoint);

//             if (IsPointInBookBounds(localPoint)) {
//                 if (isPinching && !pageDragging) {
//                     OnHandGrabPage(projectedPoint, hand);
//                 }
//             }
//         }
//     }

//     bool IsPointInBookBounds( Vector3 localPoint ) {
//         float halfWidth = BookPanel.rect.width / 2;
//         float halfHeight = BookPanel.rect.height / 2;
//         return Mathf.Abs(localPoint.x) <= halfWidth && Mathf.Abs(localPoint.y) <= halfHeight;
//     }

//     OVRHand GetActiveHand() {
//         if (rightHand != null && rightHand.IsTracked && rightHand.HandConfidence == OVRHand.TrackingConfidence.High)
//             return rightHand;
//         if (leftHand != null && leftHand.IsTracked && leftHand.HandConfidence == OVRHand.TrackingConfidence.High)
//             return leftHand;
//         return null;
//     }

//     void OnHandGrabPage( Vector3 worldPoint, OVRHand hand ) {
//         Vector3 localPoint = BookPanel.InverseTransformPoint(worldPoint);

//         if (localPoint.x > 0) {
//             if (currentPage >= bookPages.Length) return;
//             isGrippingRightPage = true;
//             isPinchGrabbing = true;
//             OnVRDragRightPage(worldPoint);
//         } else {
//             if (currentPage <= 0) return;
//             isGrippingLeftPage = true;
//             isPinchGrabbing = true;
//             OnVRDragLeftPage(worldPoint);
//         }

//         lastHandHitPoint = worldPoint;
//         Debug.Log($"✓ Hand grabbed {(localPoint.x > 0 ? "right" : "left")} page");
//     }

//     void OnHandRelease() {
//         isPinchGrabbing = false;
//         ReleasePage();
//         Debug.Log("✓ Hand released page");
//     }

//     void HandleControllerInput() {
//         if (controllerAnchor == null || !IsControllerValid()) return;

//         bool triggerPressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, vrController);
//         bool triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, vrController);
//         bool triggerUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, vrController);

//         Ray ray = GetControllerRay();
//         RaycastHit hit;

//         if (Physics.Raycast(ray, out hit, rayDistance, bookLayer)) {
//             if (triggerDown && !pageDragging) {
//                 Vector3 localPoint = BookPanel.InverseTransformPoint(hit.point);
//                 if (localPoint.x > 0) {
//                     isGrippingRightPage = true;
//                     OnVRDragRightPage(hit.point);
//                 } else {
//                     isGrippingLeftPage = true;
//                     OnVRDragLeftPage(hit.point);
//                 }
//             }
//         }

//         if (triggerUp && pageDragging) {
//             OnVRRelease();
//         }
//     }

//     bool IsControllerValid() {
//         return !float.IsNaN(controllerAnchor.position.x) &&
//                !float.IsNaN(controllerAnchor.position.y) &&
//                !float.IsNaN(controllerAnchor.position.z);
//     }

//     Ray GetControllerRay() {
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

//         // Controller ray
//         if (debugLineRenderer != null && controllerAnchor != null && IsControllerValid()) {
//             Ray ray = GetControllerRay();
//             debugLineRenderer.SetPosition(0, ray.origin);
//             debugLineRenderer.SetPosition(1, ray.origin + ray.direction * rayDistance);
//             debugLineRenderer.enabled = !isHandTrackingActive;
//         }

//         // Hand tracking ray - ✅ FIXED: Get finger tip fresh
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

//     public Vector3 TransformVRPoint( Vector3 worldPoint ) {
//         Vector2 localPos = BookPanel.InverseTransformPoint(worldPoint);
//         return localPos;
//     }

//     public void UpdateBook() {
//         Ray ray;
//         Vector3 targetPoint;

//         if (isHandTrackingActive && isPinchGrabbing) {
//             // ✅ FIXED: Get finger tip fresh each frame
//             OVRHand activeHand = GetActiveHand();
//             Transform fingerTip = GetFingerTip(activeHand);

//             if (fingerTip != null) {
//                 targetPoint = TransformVRPoint(bookPlane.ClosestPointOnPlane(fingerTip.position));
//             } else {
//                 targetPoint = f;
//             }
//         } else {
//             ray = GetControllerRay();
//             RaycastHit hit;
//             if (Physics.Raycast(ray, out hit, rayDistance, bookLayer)) {
//                 targetPoint = TransformVRPoint(hit.point);
//             } else {
//                 targetPoint = f;
//             }
//         }

//         f = Vector3.Lerp(f, targetPoint, Time.deltaTime * 10);

//         if (mode == FlipMode.RightToLeft)
//             UpdateBookRTLToPoint(f);
//         else
//             UpdateBookLTRToPoint(f);
//     }

//     public void OnVRDragRightPage( Vector3 worldPoint ) {
//         if (currentPage >= bookPages.Length) return;
//         Vector3 p = TransformVRPoint(worldPoint);
//         DragRightPageToPoint(p);
//     }

//     public void OnVRDragLeftPage( Vector3 worldPoint ) {
//         if (currentPage <= 0) return;
//         Vector3 p = TransformVRPoint(worldPoint);
//         DragLeftPageToPoint(p);
//     }

//     public void OnVRRelease() {
//         isGrippingRightPage = false;
//         isGrippingLeftPage = false;
//         ReleasePage();
//     }

//     void OnDrawGizmos() {
//         if (!showDebugRay) return;

//         // Controller ray
//         if (controllerAnchor != null && IsControllerValid() && !isHandTrackingActive) {
//             Gizmos.color = Color.red;
//             Gizmos.DrawRay(controllerAnchor.position, controllerAnchor.forward * rayDistance);
//             Gizmos.DrawWireSphere(controllerAnchor.position, 0.02f);
//         }

//         // Hand tracking ray - ✅ FIXED: Get finger tip fresh
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

//         // Draw book plane
//         if (BookPanel != null) {
//             Gizmos.color = Color.yellow;
//             Gizmos.matrix = BookPanel.transform.localToWorldMatrix;
//             Gizmos.DrawWireCube(Vector3.zero, new Vector3(BookPanel.rect.width, BookPanel.rect.height, 0.01f));
//         }
//     }

//     // === ORIGINAL BOOK FUNCTIONS ===
//     // (Keep all existing UpdateBookLTRToPoint, UpdateBookRTLToPoint, DragRightPageToPoint, etc.)

//     public void UpdateBookLTRToPoint( Vector3 followLocation ) {
//         mode = FlipMode.LeftToRight;
//         f = followLocation;
//         ShadowLTR.transform.SetParent(ClippingPlane.transform, true);
//         ShadowLTR.transform.localPosition = Vector3.zero;
//         ShadowLTR.transform.localEulerAngles = Vector3.zero;
//         Left.transform.SetParent(ClippingPlane.transform, true);
//         Right.transform.SetParent(BookPanel.transform, true);
//         Right.transform.localEulerAngles = Vector3.zero;
//         LeftNext.transform.SetParent(BookPanel.transform, true);

//         c = Calc_C_Position(followLocation);
//         Vector3 t1;
//         float clipAngle = CalcClipAngle(c, ebl, out t1);
//         clipAngle = (clipAngle + 180) % 180;

//         ClippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
//         ClippingPlane.transform.position = BookPanel.TransformPoint(t1);
//         Left.transform.position = BookPanel.TransformPoint(c);

//         float C_T1_dy = t1.y - c.y;
//         float C_T1_dx = t1.x - c.x;
//         float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
//         Left.transform.localEulerAngles = new Vector3(0, 0, C_T1_Angle - 90 - clipAngle);

//         NextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
//         NextPageClip.transform.position = BookPanel.TransformPoint(t1);
//         LeftNext.transform.SetParent(NextPageClip.transform, true);
//         Right.transform.SetParent(ClippingPlane.transform, true);
//         Right.transform.SetAsFirstSibling();
//         ShadowLTR.rectTransform.SetParent(Left.rectTransform, true);
//     }

//     public void UpdateBookRTLToPoint( Vector3 followLocation ) {
//         mode = FlipMode.RightToLeft;
//         f = followLocation;
//         Shadow.transform.SetParent(ClippingPlane.transform, true);
//         Shadow.transform.localPosition = Vector3.zero;
//         Shadow.transform.localEulerAngles = Vector3.zero;
//         Right.transform.SetParent(ClippingPlane.transform, true);
//         Left.transform.SetParent(BookPanel.transform, true);
//         Left.transform.localEulerAngles = Vector3.zero;
//         RightNext.transform.SetParent(BookPanel.transform, true);

//         c = Calc_C_Position(followLocation);
//         Vector3 t1;
//         float clipAngle = CalcClipAngle(c, ebr, out t1);
//         if (clipAngle > -90) clipAngle += 180;

//         ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);
//         ClippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
//         ClippingPlane.transform.position = BookPanel.TransformPoint(t1);
//         Right.transform.position = BookPanel.TransformPoint(c);

//         float C_T1_dy = t1.y - c.y;
//         float C_T1_dx = t1.x - c.x;
//         float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
//         Right.transform.localEulerAngles = new Vector3(0, 0, C_T1_Angle - (clipAngle + 90));

//         NextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
//         NextPageClip.transform.position = BookPanel.TransformPoint(t1);
//         RightNext.transform.SetParent(NextPageClip.transform, true);
//         Left.transform.SetParent(ClippingPlane.transform, true);
//         Left.transform.SetAsFirstSibling();
//         Shadow.rectTransform.SetParent(Right.rectTransform, true);
//     }

//     float CalcClipAngle( Vector3 c, Vector3 bookCorner, out Vector3 t1 ) {
//         Vector3 t0 = (c + bookCorner) / 2;
//         float T0_CORNER_dy = bookCorner.y - t0.y;
//         float T0_CORNER_dx = bookCorner.x - t0.x;
//         float T0_CORNER_Angle = Mathf.Atan2(T0_CORNER_dy, T0_CORNER_dx);
//         float T1_X = t0.x - T0_CORNER_dy * Mathf.Tan(T0_CORNER_Angle);
//         T1_X = normalizeT1X(T1_X, bookCorner, sb);
//         t1 = new Vector3(T1_X, sb.y, 0);
//         float T0_T1_dy = t1.y - t0.y;
//         float T0_T1_dx = t1.x - t0.x;
//         return Mathf.Atan2(T0_T1_dy, T0_T1_dx) * Mathf.Rad2Deg;
//     }

//     float normalizeT1X( float t1, Vector3 corner, Vector3 sb ) {
//         if (t1 > sb.x && sb.x > corner.x) return sb.x;
//         if (t1 < sb.x && sb.x < corner.x) return sb.x;
//         return t1;
//     }

//     Vector3 Calc_C_Position( Vector3 followLocation ) {
//         f = followLocation;
//         float F_SB_dy = f.y - sb.y;
//         float F_SB_dx = f.x - sb.x;
//         float F_SB_Angle = Mathf.Atan2(F_SB_dy, F_SB_dx);
//         Vector3 r1 = new Vector3(radius1 * Mathf.Cos(F_SB_Angle), radius1 * Mathf.Sin(F_SB_Angle), 0) + sb;
//         float F_SB_distance = Vector2.Distance(f, sb);
//         Vector3 c = (F_SB_distance < radius1) ? f : r1;

//         float F_ST_dy = c.y - st.y;
//         float F_ST_dx = c.x - st.x;
//         float F_ST_Angle = Mathf.Atan2(F_ST_dy, F_ST_dx);
//         Vector3 r2 = new Vector3(radius2 * Mathf.Cos(F_ST_Angle), radius2 * Mathf.Sin(F_ST_Angle), 0) + st;
//         float C_ST_distance = Vector2.Distance(c, st);
//         if (C_ST_distance > radius2) c = r2;
//         return c;
//     }

//     public void DragRightPageToPoint( Vector3 point ) {
//         if (currentPage >= bookPages.Length) return;
//         pageDragging = true;
//         mode = FlipMode.RightToLeft;
//         f = point;

//         NextPageClip.rectTransform.pivot = new Vector2(0, 0.12f);
//         ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);

//         Left.gameObject.SetActive(true);
//         Left.rectTransform.pivot = new Vector2(0, 0);
//         Left.transform.position = RightNext.transform.position;
//         Left.transform.eulerAngles = Vector3.zero;
//         Left.sprite = (currentPage < bookPages.Length) ? bookPages[currentPage] : background;
//         Left.transform.SetAsFirstSibling();

//         Right.gameObject.SetActive(true);
//         Right.transform.position = RightNext.transform.position;
//         Right.transform.eulerAngles = Vector3.zero;
//         Right.sprite = (currentPage < bookPages.Length - 1) ? bookPages[currentPage + 1] : background;
//         RightNext.sprite = (currentPage < bookPages.Length - 2) ? bookPages[currentPage + 2] : background;

//         LeftNext.transform.SetAsFirstSibling();
//         if (enableShadowEffect) Shadow.gameObject.SetActive(true);
//         UpdateBookRTLToPoint(f);
//     }

//     public void DragLeftPageToPoint( Vector3 point ) {
//         if (currentPage <= 0) return;
//         pageDragging = true;
//         mode = FlipMode.LeftToRight;
//         f = point;

//         NextPageClip.rectTransform.pivot = new Vector2(1, 0.12f);
//         ClippingPlane.rectTransform.pivot = new Vector2(0, 0.35f);

//         Right.gameObject.SetActive(true);
//         Right.transform.position = LeftNext.transform.position;
//         Right.sprite = bookPages[currentPage - 1];
//         Right.transform.eulerAngles = Vector3.zero;
//         Right.transform.SetAsFirstSibling();

//         Left.gameObject.SetActive(true);
//         Left.rectTransform.pivot = new Vector2(1, 0);
//         Left.transform.position = LeftNext.transform.position;
//         Left.transform.eulerAngles = Vector3.zero;
//         Left.sprite = (currentPage >= 2) ? bookPages[currentPage - 2] : background;
//         LeftNext.sprite = (currentPage >= 3) ? bookPages[currentPage - 3] : background;

//         RightNext.transform.SetAsFirstSibling();
//         if (enableShadowEffect) ShadowLTR.gameObject.SetActive(true);
//         UpdateBookLTRToPoint(f);
//     }

//     public void ReleasePage() {
//         if (pageDragging) {
//             pageDragging = false;
//             float distanceToLeft = Vector2.Distance(c, ebl);
//             float distanceToRight = Vector2.Distance(c, ebr);
//             if (distanceToRight < distanceToLeft && mode == FlipMode.RightToLeft)
//                 TweenBack();
//             else if (distanceToRight > distanceToLeft && mode == FlipMode.LeftToRight)
//                 TweenBack();
//             else
//                 TweenForward();
//         }
//     }

//     Coroutine currentCoroutine;

//     void UpdateSprites() {
//         LeftNext.sprite = (currentPage > 0 && currentPage <= bookPages.Length) ? bookPages[currentPage - 1] : background;
//         RightNext.sprite = (currentPage >= 0 && currentPage < bookPages.Length) ? bookPages[currentPage] : background;
//     }

//     public void TweenForward() {
//         if (mode == FlipMode.RightToLeft)
//             currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f, () => { Flip(); }));
//         else
//             currentCoroutine = StartCoroutine(TweenTo(ebr, 0.15f, () => { Flip(); }));
//     }

//     void Flip() {
//         if (mode == FlipMode.RightToLeft)
//             currentPage += 2;
//         else
//             currentPage -= 2;

//         LeftNext.transform.SetParent(BookPanel.transform, true);
//         Left.transform.SetParent(BookPanel.transform, true);
//         LeftNext.transform.SetParent(BookPanel.transform, true);
//         Left.gameObject.SetActive(false);
//         Right.gameObject.SetActive(false);
//         Right.transform.SetParent(BookPanel.transform, true);
//         RightNext.transform.SetParent(BookPanel.transform, true);
//         UpdateSprites();
//         Shadow.gameObject.SetActive(false);
//         ShadowLTR.gameObject.SetActive(false);
//         if (OnFlip != null)
//             OnFlip.Invoke();
//     }

//     public void TweenBack() {
//         if (mode == FlipMode.RightToLeft) {
//             currentCoroutine = StartCoroutine(TweenTo(ebr, 0.15f, () => {
//                 UpdateSprites();
//                 RightNext.transform.SetParent(BookPanel.transform);
//                 Right.transform.SetParent(BookPanel.transform);
//                 Left.gameObject.SetActive(false);
//                 Right.gameObject.SetActive(false);
//                 pageDragging = false;
//             }));
//         } else {
//             currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f, () => {
//                 UpdateSprites();
//                 LeftNext.transform.SetParent(BookPanel.transform);
//                 Left.transform.SetParent(BookPanel.transform);
//                 Left.gameObject.SetActive(false);
//                 Right.gameObject.SetActive(false);
//                 pageDragging = false;
//             }));
//         }
//     }

//     public IEnumerator TweenTo( Vector3 to, float duration, System.Action onFinish ) {
//         int steps = (int)(duration / 0.025f);
//         Vector3 displacement = (to - f) / steps;
//         for (int i = 0; i < steps - 1; i++) {
//             if (mode == FlipMode.RightToLeft)
//                 UpdateBookRTLToPoint(f + displacement);
//             else
//                 UpdateBookLTRToPoint(f + displacement);
//             yield return new WaitForSeconds(0.025f);
//         }
//         if (onFinish != null)
//             onFinish();
//     }
// }