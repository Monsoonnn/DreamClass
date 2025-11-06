using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Events;
using Oculus.Interaction.Input;

public enum FlipMode
{
    RightToLeft,
    LeftToRight
}

[ExecuteInEditMode]
public class BookVR : MonoBehaviour
{
    [Header("VR Settings")]
    public bool enableBothControllers = true;
    public LayerMask bookLayer;
    public float rayDistance = 10f;
    public bool showDebugRay = true;

    private Transform rightControllerAnchor;
    private Transform leftControllerAnchor;
    private bool isRightControllerGripping = false;
    private bool isLeftControllerGripping = false;

    private LineRenderer rightDebugLineRenderer;
    private LineRenderer leftDebugLineRenderer;

    [Header("Hand Tracking")]
    public bool enableHandTracking = true;
    public OVRHand rightHand;
    public OVRHand leftHand;
    public OVRSkeleton rightHandSkeleton;
    public OVRSkeleton leftHandSkeleton;
    public float handRaycastDistance = 0.5f;
    public float grabThreshold = 0.15f;

    [Header("Poke Interaction")]
    public bool enablePokeInteraction = true;
    public float pokeDepthThreshold = 0.02f;
    public float pinchThresholdToGrab = 0.7f;

    [Header("Book Settings")]
    public Canvas canvas;
    [SerializeField] RectTransform BookPanel;
    public bool interactable = true;

    [Header("Sprite Manager")]
    public BookSpriteManager spriteManager;

    [Header("Auto Flip Page")]
    public AutoFlipVR autoFlipVR;

    public int CurrentPage
    {
        get { return spriteManager != null ? spriteManager.CurrentPage : 0; }
        set { if (spriteManager != null) spriteManager.CurrentPage = value; }
    }
    public int TotalPageCount { get { return spriteManager != null ? spriteManager.TotalPageCount : 0; } }
    public Vector3 EndBottomLeft { get { return ebl; } }
    public Vector3 EndBottomRight { get { return ebr; } }
    public float Height { get { return BookPanel.rect.height; } }

    public Image ClippingPlane;
    public Image NextPageClip;
    public UnityEvent OnFlip;

    float radius1, radius2;
    Vector3 sb, st, c, ebr, ebl, f;
    bool pageDragging = false;
    FlipMode mode;

    private OVRCameraRig cameraRig;
    private Transform controllerAnchor;
    private bool isGrippingRightPage = false;
    private bool isGrippingLeftPage = false;
    private LineRenderer debugLineRenderer;
    private LineRenderer handDebugLineRenderer;

    public const OVRSkeleton.BoneId INDEX_TIP_BONE = OVRSkeleton.BoneId.Hand_IndexTip;
    private bool isHandTrackingActive = false;
    private bool isPinchGrabbing = false;
    private Vector3 lastHandHitPoint;
    private Plane bookPlane;

    void Start()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!canvas) Debug.LogError("Book should be a child to canvas");

        if (spriteManager == null)
        {
            spriteManager = GetComponentInChildren<BookSpriteManager>();
            if (spriteManager == null)
            {
                Debug.LogError("BookSpriteManager not found! Please add it to the GameObject.");
            }
        }

        CalcCurlCriticalPoints();
        StartCoroutine(DelayedVRInitialization());

        float pageWidth = BookPanel.rect.width / 2.0f;
        float pageHeight = BookPanel.rect.height;
        NextPageClip.rectTransform.sizeDelta = new Vector2(pageWidth, pageHeight + pageHeight * 2);
        ClippingPlane.rectTransform.sizeDelta = new Vector2(pageWidth * 2 + pageHeight, pageHeight + pageHeight * 2);

        if (showDebugRay)
        {
            SetupDebugLineRenderer();
            SetupHandDebugLineRenderer();
        }

        bookPlane = new Plane(BookPanel.transform.forward, BookPanel.transform.position);
    }

    IEnumerator DelayedVRInitialization()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(2f);
        InitializeVR();
    }

    void InitializeVR()
    {
        cameraRig = FindObjectOfType<OVRCameraRig>();
        if (cameraRig != null)
        {
            rightControllerAnchor = cameraRig.rightHandAnchor;
            leftControllerAnchor = cameraRig.leftHandAnchor;

            if (enableHandTracking) InitializeHandTracking();
        }
        else
        {
            Debug.LogError("OVRCameraRig not found!");
        }
    }

    void InitializeHandTracking()
    {
        var scoure = GameObject.FindObjectsOfType<FromOVRHandDataSource>();

        if (rightHand == null)
            rightHand = scoure[1].GetComponent<OVRHand>();
        if (leftHand == null)
            leftHand = scoure[0].GetComponent<OVRHand>();

        var scoureSkeleton = GameObject.FindObjectsOfType<OVRSkeleton>();

        if (rightHandSkeleton == null && rightHand != null)
            rightHandSkeleton = scoureSkeleton[0];
        if (leftHandSkeleton == null && leftHand != null)
            leftHandSkeleton = scoureSkeleton[1];

        Debug.Log(rightHand != null || leftHand != null ? "✓ Hand tracking initialized" : "Hand tracking not found");
    }

    void SetupDebugLineRenderer()
    {
        GameObject lineObj = new GameObject("ControllerDebugRay");
        lineObj.transform.SetParent(transform);
        debugLineRenderer = lineObj.AddComponent<LineRenderer>();
        debugLineRenderer.startWidth = 0.005f;
        debugLineRenderer.endWidth = 0.005f;
        debugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        debugLineRenderer.startColor = Color.red;
        debugLineRenderer.endColor = Color.yellow;
        debugLineRenderer.positionCount = 2;
    }

    void SetupHandDebugLineRenderer()
    {
        GameObject lineObj = new GameObject("HandDebugRay");
        lineObj.transform.SetParent(transform);
        handDebugLineRenderer = lineObj.AddComponent<LineRenderer>();
        handDebugLineRenderer.startWidth = 0.003f;
        handDebugLineRenderer.endWidth = 0.003f;
        handDebugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        handDebugLineRenderer.startColor = Color.cyan;
        handDebugLineRenderer.endColor = Color.blue;
        handDebugLineRenderer.positionCount = 2;
    }

    void CalcCurlCriticalPoints()
    {
        sb = new Vector3(0, -BookPanel.rect.height / 2);
        ebr = new Vector3(BookPanel.rect.width / 2, -BookPanel.rect.height / 2);
        ebl = new Vector3(-BookPanel.rect.width / 2, -BookPanel.rect.height / 2);
        st = new Vector3(0, BookPanel.rect.height / 2);
        radius1 = Vector2.Distance(sb, ebr);
        float pageWidth = BookPanel.rect.width / 2.0f;
        float pageHeight = BookPanel.rect.height;
        radius2 = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
    }

    void Update()
    {
        if (!interactable) return;

        bookPlane.SetNormalAndPosition(BookPanel.transform.forward, BookPanel.transform.position);
        isHandTrackingActive = IsHandTrackingActive();

        if (isHandTrackingActive && enableHandTracking)
        {
            HandleHandTrackingInput();
        }
        else
        {
            HandleControllerInput();
        }

        if (pageDragging)
        {
            UpdateBook();
        }

        UpdateDebugRays();
    }

    bool IsHandTrackingActive()
    {
        if (rightHand != null && rightHand.IsTracked && rightHand.HandConfidence == OVRHand.TrackingConfidence.High)
            return true;
        if (leftHand != null && leftHand.IsTracked && leftHand.HandConfidence == OVRHand.TrackingConfidence.High)
            return true;
        return false;
    }

    Transform GetFingerTip(OVRHand hand)
    {
        if (hand == null) return null;

        OVRSkeleton skeleton = (hand == rightHand) ? rightHandSkeleton : leftHandSkeleton;
        if (skeleton == null || skeleton.Bones == null || skeleton.Bones.Count == 0) return null;

        foreach (var bone in skeleton.Bones)
        {
            if (bone.Id == INDEX_TIP_BONE)
            {
                return bone.Transform;
            }
        }
        return null;
    }

    void HandleHandTrackingInput()
    {
        OVRHand activeHand = GetActiveHand();
        if (activeHand == null) return;

        Transform fingerTip = GetFingerTip(activeHand);
        if (fingerTip == null) return;

        float pinchStrength = activeHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        bool isPinching = pinchStrength > pinchThresholdToGrab;
        bool isGrabbing = IsHandGrabbing(activeHand);

        Ray fingerRay = new Ray(fingerTip.position, fingerTip.forward);
        RaycastHit hit;

        if (Physics.Raycast(fingerRay, out hit, handRaycastDistance, bookLayer))
        {
            if ((isPinching || isGrabbing) && !pageDragging)
            {
                OnHandGrabPage(hit.point, activeHand);
            }
        }

        if (enablePokeInteraction)
        {
            HandlePokeInteraction(fingerTip, activeHand, isPinching || isGrabbing);
        }

        if (!isPinching && !isGrabbing && pageDragging && isPinchGrabbing)
        {
            OnHandRelease();
        }
    }

    bool IsHandGrabbing(OVRHand hand)
    {
        if (hand == null) return false;

        float middleCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        float ringCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
        float pinkyCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);
        float thumbCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb);
        float indexCurl = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

        float avgCurl = (middleCurl + ringCurl + pinkyCurl + thumbCurl + indexCurl) / 5f;
        return avgCurl > grabThreshold;
    }

    void HandlePokeInteraction(Transform fingerTip, OVRHand hand, bool isPinching)
    {
        float distanceToPlane = bookPlane.GetDistanceToPoint(fingerTip.position);

        if (Mathf.Abs(distanceToPlane) < pokeDepthThreshold)
        {
            Vector3 projectedPoint = bookPlane.ClosestPointOnPlane(fingerTip.position);
            Vector3 localPoint = BookPanel.InverseTransformPoint(projectedPoint);

            if (IsPointInBookBounds(localPoint))
            {
                if (isPinching && !pageDragging)
                {
                    OnHandGrabPage(projectedPoint, hand);
                }
            }
        }
    }

    bool IsPointInBookBounds(Vector3 localPoint)
    {
        float halfWidth = BookPanel.rect.width / 2;
        float halfHeight = BookPanel.rect.height / 2;
        return Mathf.Abs(localPoint.x) <= halfWidth && Mathf.Abs(localPoint.y) <= halfHeight;
    }

    OVRHand GetActiveHand()
    {
        if (rightHand != null && rightHand.IsTracked && rightHand.HandConfidence == OVRHand.TrackingConfidence.High)
            return rightHand;
        if (leftHand != null && leftHand.IsTracked && leftHand.HandConfidence == OVRHand.TrackingConfidence.High)
            return leftHand;
        return null;
    }

    void OnHandGrabPage(Vector3 worldPoint, OVRHand hand)
    {
        Vector3 localPoint = BookPanel.InverseTransformPoint(worldPoint);

        if (localPoint.x > 0)
        {
            if (!spriteManager.CanFlipRight()) return;
            isGrippingRightPage = true;
            isPinchGrabbing = true;
            OnVRDragRightPage(worldPoint);
        }
        else
        {
            if (!spriteManager.CanFlipLeft()) return;
            isGrippingLeftPage = true;
            isPinchGrabbing = true;
            OnVRDragLeftPage(worldPoint);
        }

        lastHandHitPoint = worldPoint;

    }

    void OnHandRelease()
    {
        isPinchGrabbing = false;
        ReleasePage();

    }

    void HandleControllerInput()
    {
        if (!IsControllerValid(rightControllerAnchor) && !IsControllerValid(leftControllerAnchor))
            return;

        // Xử lý tay phải - CẢ 2 HƯỚNG
        if (IsControllerValid(rightControllerAnchor))
        {
            HandleSingleController(
                OVRInput.Controller.RTouch,
                rightControllerAnchor,
                ref isRightControllerGripping
            );
        }

        // Xử lý tay trái - CẢ 2 HƯỚNG (nếu bật chế độ 2 tay)
        if (enableBothControllers && IsControllerValid(leftControllerAnchor))
        {
            HandleSingleController(
                OVRInput.Controller.LTouch,
                leftControllerAnchor,
                ref isLeftControllerGripping
            );
        }
    }

    void HandleSingleController(OVRInput.Controller controller, Transform anchor, ref bool isGripping)
    {
        bool triggerPressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
        bool triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller);
        bool triggerUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller);

        Ray ray = new Ray(anchor.position, anchor.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, bookLayer))
        {
            if (triggerDown && !pageDragging)
            {
                Vector3 localPoint = BookPanel.InverseTransformPoint(hit.point);

                // CẢ 2 CONTROLLER ĐỀU CÓ THỂ LẬT CẢ 2 HƯỚNG
                // Dựa vào vị trí chạm (localPoint.x) để quyết định hướng lật
                if (localPoint.x > 0)
                {
                    // Chạm vào trang phải -> lật từ phải sang trái
                    if (!spriteManager.CanFlipRight()) return;
                    isGripping = true;
                    OnVRDragRightPage(hit.point);

                }
                else
                {
                    // Chạm vào trang trái -> lật từ trái sang phải
                    if (!spriteManager.CanFlipLeft()) return;
                    isGripping = true;
                    OnVRDragLeftPage(hit.point);

                }
            }
        }

        if (triggerUp && isGripping)
        {
            isGripping = false;
            OnVRRelease();
        }
    }

    bool IsControllerValid(Transform anchor)
    {
        if (anchor == null) return false;
        return !float.IsNaN(anchor.position.x) &&
               !float.IsNaN(anchor.position.y) &&
               !float.IsNaN(anchor.position.z);
    }

    bool IsControllerValid()
    {
        return !float.IsNaN(controllerAnchor.position.x) &&
               !float.IsNaN(controllerAnchor.position.y) &&
               !float.IsNaN(controllerAnchor.position.z);
    }

    Ray GetControllerRay()
    {
        if (controllerAnchor != null && IsControllerValid())
        {
            return new Ray(controllerAnchor.position, controllerAnchor.forward);
        }
        if (Camera.main != null)
        {
            return new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        }
        return new Ray(Vector3.zero, Vector3.forward);
    }

    void UpdateDebugRays()
    {
        if (!showDebugRay) return;

        if (debugLineRenderer != null && controllerAnchor != null && IsControllerValid())
        {
            Ray ray = GetControllerRay();
            debugLineRenderer.SetPosition(0, ray.origin);
            debugLineRenderer.SetPosition(1, ray.origin + ray.direction * rayDistance);
            debugLineRenderer.enabled = !isHandTrackingActive;
        }

        if (handDebugLineRenderer != null && isHandTrackingActive)
        {
            OVRHand activeHand = GetActiveHand();
            Transform fingerTip = GetFingerTip(activeHand);

            if (fingerTip != null)
            {
                handDebugLineRenderer.SetPosition(0, fingerTip.position);
                handDebugLineRenderer.SetPosition(1, fingerTip.position + fingerTip.forward * handRaycastDistance);
                handDebugLineRenderer.enabled = true;
            }
            else
            {
                handDebugLineRenderer.enabled = false;
            }
        }
        else if (handDebugLineRenderer != null)
        {
            handDebugLineRenderer.enabled = false;
        }
    }

    public Vector3 TransformVRPoint(Vector3 worldPoint)
    {
        Vector2 localPos = BookPanel.InverseTransformPoint(worldPoint);
        return localPos;
    }

    public void UpdateBook()
    {
        Ray ray;
        Vector3 targetPoint;

        if (isHandTrackingActive && isPinchGrabbing)
        {
            OVRHand activeHand = GetActiveHand();
            Transform fingerTip = GetFingerTip(activeHand);
            if (fingerTip != null)
            {
                targetPoint = TransformVRPoint(bookPlane.ClosestPointOnPlane(fingerTip.position));
            }
            else
            {
                targetPoint = f;
            }
        }
        else
        {
            Transform activeController = null;

            if (isRightControllerGripping && IsControllerValid(rightControllerAnchor))
                activeController = rightControllerAnchor;
            else if (isLeftControllerGripping && IsControllerValid(leftControllerAnchor))
                activeController = leftControllerAnchor;

            if (activeController != null)
            {
                ray = new Ray(activeController.position, activeController.forward);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, rayDistance, bookLayer))
                {
                    targetPoint = TransformVRPoint(hit.point);
                }
                else
                {
                    targetPoint = f;
                }
            }
            else
            {
                targetPoint = f;
            }
        }

        f = Vector3.Lerp(f, targetPoint, Time.deltaTime * 10);

        if (mode == FlipMode.RightToLeft)
            UpdateBookRTLToPoint(f);
        else
            UpdateBookLTRToPoint(f);
    }

    public void OnVRDragRightPage(Vector3 worldPoint)
    {
        if (!spriteManager.CanFlipRight()) return;
        Vector3 p = TransformVRPoint(worldPoint);
        DragRightPageToPoint(p);
    }

    public void OnVRDragLeftPage(Vector3 worldPoint)
    {
        if (!spriteManager.CanFlipLeft()) return;
        Vector3 p = TransformVRPoint(worldPoint);
        DragLeftPageToPoint(p);
    }

    public void OnVRRelease()
    {
        isGrippingRightPage = false;
        isGrippingLeftPage = false;
        ReleasePage();
    }

    void OnDrawGizmos()
    {
        if (!showDebugRay) return;

        if (controllerAnchor != null && IsControllerValid() && !isHandTrackingActive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(controllerAnchor.position, controllerAnchor.forward * rayDistance);
            Gizmos.DrawWireSphere(controllerAnchor.position, 0.02f);
        }

        if (isHandTrackingActive)
        {
            OVRHand activeHand = GetActiveHand();
            Transform fingerTip = GetFingerTip(activeHand);

            if (fingerTip != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(fingerTip.position, fingerTip.forward * handRaycastDistance);
                Gizmos.DrawWireSphere(fingerTip.position, 0.01f);

                if (enablePokeInteraction)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(fingerTip.position, pokeDepthThreshold);
                }
            }
        }

        if (BookPanel != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = BookPanel.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(BookPanel.rect.width, BookPanel.rect.height, 0.01f));
        }
    }

    // === BOOK ANIMATION FUNCTIONS ===

    public void UpdateBookLTRToPoint(Vector3 followLocation)
    {
        mode = FlipMode.LeftToRight;
        f = followLocation;

        spriteManager.ShadowLTR.transform.SetParent(ClippingPlane.transform, true);
        spriteManager.ShadowLTR.transform.localPosition = Vector3.zero;
        spriteManager.ShadowLTR.transform.localEulerAngles = Vector3.zero;
        spriteManager.Left.transform.SetParent(ClippingPlane.transform, true);
        spriteManager.Right.transform.SetParent(BookPanel.transform, true);
        spriteManager.Right.transform.localEulerAngles = Vector3.zero;
        spriteManager.LeftNext.transform.SetParent(BookPanel.transform, true);

        c = Calc_C_Position(followLocation);
        Vector3 t1;
        float clipAngle = CalcClipAngle(c, ebl, out t1);
        clipAngle = (clipAngle + 180) % 180;

        ClippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
        ClippingPlane.transform.position = BookPanel.TransformPoint(t1);
        spriteManager.Left.transform.position = BookPanel.TransformPoint(c);

        float C_T1_dy = t1.y - c.y;
        float C_T1_dx = t1.x - c.x;
        float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
        spriteManager.Left.transform.localEulerAngles = new Vector3(0, 0, C_T1_Angle - 90 - clipAngle);

        NextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
        NextPageClip.transform.position = BookPanel.TransformPoint(t1);
        spriteManager.LeftNext.transform.SetParent(NextPageClip.transform, true);
        spriteManager.Right.transform.SetParent(ClippingPlane.transform, true);
        spriteManager.Right.transform.SetAsFirstSibling();
        spriteManager.ShadowLTR.rectTransform.SetParent(spriteManager.Left.rectTransform, true);
    }

    public void UpdateBookRTLToPoint(Vector3 followLocation)
    {
        mode = FlipMode.RightToLeft;
        f = followLocation;

        spriteManager.Shadow.transform.SetParent(ClippingPlane.transform, true);
        spriteManager.Shadow.transform.localPosition = Vector3.zero;
        spriteManager.Shadow.transform.localEulerAngles = Vector3.zero;
        spriteManager.Right.transform.SetParent(ClippingPlane.transform, true);
        spriteManager.Left.transform.SetParent(BookPanel.transform, true);
        spriteManager.Left.transform.localEulerAngles = Vector3.zero;
        spriteManager.RightNext.transform.SetParent(BookPanel.transform, true);

        c = Calc_C_Position(followLocation);
        Vector3 t1;
        float clipAngle = CalcClipAngle(c, ebr, out t1);
        if (clipAngle > -90) clipAngle += 180;

        ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);
        ClippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
        ClippingPlane.transform.position = BookPanel.TransformPoint(t1);
        spriteManager.Right.transform.position = BookPanel.TransformPoint(c);

        float C_T1_dy = t1.y - c.y;
        float C_T1_dx = t1.x - c.x;
        float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
        spriteManager.Right.transform.localEulerAngles = new Vector3(0, 0, C_T1_Angle - (clipAngle + 90));

        NextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
        NextPageClip.transform.position = BookPanel.TransformPoint(t1);
        spriteManager.RightNext.transform.SetParent(NextPageClip.transform, true);
        spriteManager.Left.transform.SetParent(ClippingPlane.transform, true);
        spriteManager.Left.transform.SetAsFirstSibling();
        spriteManager.Shadow.rectTransform.SetParent(spriteManager.Right.rectTransform, true);
    }

    float CalcClipAngle(Vector3 c, Vector3 bookCorner, out Vector3 t1)
    {
        Vector3 t0 = (c + bookCorner) / 2;
        float T0_CORNER_dy = bookCorner.y - t0.y;
        float T0_CORNER_dx = bookCorner.x - t0.x;
        float T0_CORNER_Angle = Mathf.Atan2(T0_CORNER_dy, T0_CORNER_dx);
        float T1_X = t0.x - T0_CORNER_dy * Mathf.Tan(T0_CORNER_Angle);
        T1_X = normalizeT1X(T1_X, bookCorner, sb);
        t1 = new Vector3(T1_X, sb.y, 0);
        float T0_T1_dy = t1.y - t0.y;
        float T0_T1_dx = t1.x - t0.x;
        return Mathf.Atan2(T0_T1_dy, T0_T1_dx) * Mathf.Rad2Deg;
    }

    float normalizeT1X(float t1, Vector3 corner, Vector3 sb)
    {
        if (t1 > sb.x && sb.x > corner.x) return sb.x;
        if (t1 < sb.x && sb.x < corner.x) return sb.x;
        return t1;
    }

    Vector3 Calc_C_Position(Vector3 followLocation)
    {
        f = followLocation;
        float F_SB_dy = f.y - sb.y;
        float F_SB_dx = f.x - sb.x;
        float F_SB_Angle = Mathf.Atan2(F_SB_dy, F_SB_dx);
        Vector3 r1 = new Vector3(radius1 * Mathf.Cos(F_SB_Angle), radius1 * Mathf.Sin(F_SB_Angle), 0) + sb;
        float F_SB_distance = Vector2.Distance(f, sb);
        Vector3 c = (F_SB_distance < radius1) ? f : r1;

        float F_ST_dy = c.y - st.y;
        float F_ST_dx = c.x - st.x;
        float F_ST_Angle = Mathf.Atan2(F_ST_dy, F_ST_dx);
        Vector3 r2 = new Vector3(radius2 * Mathf.Cos(F_ST_Angle), radius2 * Mathf.Sin(F_ST_Angle), 0) + st;
        float C_ST_distance = Vector2.Distance(c, st);
        if (C_ST_distance > radius2) c = r2;
        return c;
    }

    public void DragRightPageToPoint(Vector3 point)
    {
        if (!spriteManager.CanFlipRight()) return;

        pageDragging = true;
        mode = FlipMode.RightToLeft;
        f = point;

        NextPageClip.rectTransform.pivot = new Vector2(0, 0.12f);
        ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);

        spriteManager.SetupRightPageDrag();
        UpdateBookRTLToPoint(f);
    }

    public void DragLeftPageToPoint(Vector3 point)
    {
        if (!spriteManager.CanFlipLeft()) return;

        pageDragging = true;
        mode = FlipMode.LeftToRight;
        f = point;

        NextPageClip.rectTransform.pivot = new Vector2(1, 0.12f);
        ClippingPlane.rectTransform.pivot = new Vector2(0, 0.35f);

        spriteManager.SetupLeftPageDrag();
        UpdateBookLTRToPoint(f);
    }

    public void ReleasePage()
    {
        if (pageDragging)
        {
            pageDragging = false;
            float distanceToLeft = Vector2.Distance(c, ebl);
            float distanceToRight = Vector2.Distance(c, ebr);
            if (distanceToRight < distanceToLeft && mode == FlipMode.RightToLeft)
                TweenBack();
            else if (distanceToRight > distanceToLeft && mode == FlipMode.LeftToRight)
                TweenBack();
            else
                TweenForward();
        }
    }

    Coroutine currentCoroutine;

    public void TweenForward()
    {
        if (mode == FlipMode.RightToLeft)
            currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f, () => { Flip(); }));
        else
            currentCoroutine = StartCoroutine(TweenTo(ebr, 0.15f, () => { Flip(); }));
    }

    void Flip()
    {
        spriteManager.FlipForward(mode);
        if (OnFlip != null)
            OnFlip.Invoke();
    }

    public void TweenBack()
    {
        if (mode == FlipMode.RightToLeft)
        {
            currentCoroutine = StartCoroutine(TweenTo(ebr, 0.15f, () =>
            {
                spriteManager.ResetPagesAfterTweenBack(mode, BookPanel.transform);
                pageDragging = false;
            }));
        }
        else
        {
            currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f, () =>
            {
                spriteManager.ResetPagesAfterTweenBack(mode, BookPanel.transform);
                pageDragging = false;
            }));
        }
    }

    public IEnumerator TweenTo(Vector3 to, float duration, System.Action onFinish)
    {
        int steps = (int)(duration / 0.025f);
        Vector3 displacement = (to - f) / steps;
        for (int i = 0; i < steps - 1; i++)
        {
            if (mode == FlipMode.RightToLeft)
                UpdateBookRTLToPoint(f + displacement);
            else
                UpdateBookLTRToPoint(f + displacement);
            yield return new WaitForSeconds(0.025f);
        }
        if (onFinish != null)
            onFinish();
    }

    // Set page instantly without animation
    public void SetPageInstant(int page)
    {
        // Clamp to valid range
        page = Mathf.Clamp(page, 0, TotalPageCount - 1);

        CurrentPage = page;

        // Update the displayed sprites
        spriteManager.ShowPage(CurrentPage);

        // Force UI refresh
        spriteManager.RefreshBookState();

        //Debug.Log($"[BookVR] Instantly set page to: {CurrentPage}");
    }

}