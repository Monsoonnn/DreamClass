using UnityEngine;
using System.Collections;

/// <summary>
/// Cốc có thể nghiêng để đổ nước ra vào Glass
/// Dựa trên WaterStream - có công thức chảy nước với gravity
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class PouringCup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform pourPoint; 
    [SerializeField] private GameObject liquidObject;
    [SerializeField] private LineRenderer pourStream; 
    [SerializeField] private Rigidbody rb;
    
    [Header("Cup Settings")]
    [SerializeField] private float maxCapacity = 100f;
    [SerializeField] private float currentAmount = 50f; 
    [SerializeField] private float pourRate = 20f;
    [SerializeField] private float pourAngleThreshold = 30f; 
    [SerializeField] private float maxPourAngle = 120f; 
    [SerializeField] private float waterSpeed = 2f; 
    [SerializeField] private float streamLength = 2f; 

    [Header("Visual Settings")]
    [SerializeField] private Color emptyColor = new Color(0.3f, 0.6f, 1f, 0.3f);
    [SerializeField] private Color fullColor = new Color(0.2f, 0.5f, 1f, 0.8f);
    [SerializeField] private Color streamColor = new Color(0.3f, 0.6f, 1f, 0.7f);
    [SerializeField] private float streamWidth = 0.05f;
    
    
    [Header("Water Data")]
    [SerializeField] private string waterGroupName; // Tên nhóm nước
    [SerializeField] private Material liquidColor; 

    [SerializeField] private Texture2D waterTexture30; // Texture 30% absorption
    [SerializeField] private Texture2D waterTexture80; // Texture 80% absorption
    
    [Header("Detection")]
    [SerializeField] private float detectionDistance = 3f; // Khoảng cách raycast từ pourPoint xuống
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    
    [Header("Initial Position")]
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool hasInitialPositionSaved = false;
    
    private Renderer liquidRenderer;
    private MaterialPropertyBlock propBlock;
    
    private bool isPouring = false;
    private float currentPourAngle = 0f;
    private GlassController targetGlass = null;
    private GlassController connectedGlass = null; // Glass mà cup này đã kết nối
    private BoxCollider cupCollider;
    
    void Start()
    {
        // Lưu vị trí và rotation ban đầu
        SaveInitialPosition();
        
        SetupComponents();
        UpdateLiquidVisual();
    }
    
    void Update()
    {
        UpdatePourAngle();
        CheckPouring();
        
        if (isPouring && currentAmount > 1)
        {
            DetectGlass(); // Detect trước để set targetGlass
            
            // Chỉ đổ nước nếu có Glass gần
            if (targetGlass != null && !targetGlass.IsFull() )
            {
                PourWater(); // PourWater sẽ tự kiểm tra và trừ nước
            }
            
            UpdatePourStream();
        }
        else
        {
            if (pourStream != null)
                pourStream.enabled = false;
        }

        if(currentAmount < 1)
        {
            this.gameObject.SetActive(false);
        } else
        {
            this.gameObject.SetActive(true);
        }
    }
    
    private void SetupComponents()
    {
        // Setup cup collider
        cupCollider = GetComponent<BoxCollider>();
        
        // Setup pour point (mũi cốc)
        if (pourPoint == null)
        {
            GameObject pourObj = new GameObject("PourPoint");
            pourObj.transform.SetParent(transform);
            pourObj.transform.localPosition = new Vector3(0, 0.5f, 0.3f);
            pourPoint = pourObj.transform;
        }
        
        // Setup liquid object
        if (liquidObject == null)
        {
            Debug.LogError("[PouringCup] Liquid object not assigned!");
        }
        
        liquidRenderer = liquidObject.GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        
        // Setup pour stream LineRenderer
        if (pourStream == null)
        {
            GameObject streamObj = new GameObject("PourStream");
            streamObj.transform.SetParent(transform);
            pourStream = streamObj.AddComponent<LineRenderer>();
            pourStream.material = new Material(Shader.Find("Sprites/Default"));
            pourStream.startWidth = streamWidth;
            pourStream.endWidth = streamWidth * 0.5f;
            pourStream.positionCount = 15;
            pourStream.startColor = streamColor;
            pourStream.endColor = new Color(streamColor.r, streamColor.g, streamColor.b, 0.3f);
            pourStream.useWorldSpace = true;
            pourStream.enabled = false;
        }
        
        // Setup rigidbody
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        
    }
    
    /// <summary>
    /// Tính góc nghiêng của cốc so với trục Y
    /// </summary>
    private void UpdatePourAngle()
    {
        Vector3 upDirection = transform.up;
        currentPourAngle = Vector3.Angle(Vector3.up, upDirection);
    }
    
    /// <summary>
    /// Kiểm tra xem cốc có đang nghiêng để đổ nước hay không
    /// </summary>
    private void CheckPouring()
    {
        bool shouldPour = currentAmount > 0 && 
                         currentPourAngle >= pourAngleThreshold && 
                         currentPourAngle <= maxPourAngle;
        
        if (shouldPour && !isPouring)
        {
            StartPouring();
        }
        else if (!shouldPour && isPouring)
        {
            StopPouring();
        }
    }
    
    private void StartPouring()
    {
        isPouring = true;
        
        if (debugMode)
            Debug.Log($"[PouringCup] Started pouring (angle: {currentPourAngle:F1}°)");
    }
    
    private void StopPouring()
    {
        isPouring = false;
       
        
        targetGlass = null;
        if (pourStream != null)
            pourStream.enabled = false;
            
        if (debugMode)
            Debug.Log("[PouringCup] Stopped pouring");
    }
    
    /// <summary>
    /// Đổ nước: giảm currentAmount và tính toán tốc độ dựa trên góc
    /// Chỉ trừ nước khi thực sự đổ được vào Glass
    /// </summary>
    private void PourWater()
    {
        // Tính toán tốc độ đổ dựa trên góc nghiêng
        // Khi angleThreshold → tốc độ = 0, khi maxPourAngle → tốc độ = 100%
        float angleRatio = Mathf.InverseLerp(pourAngleThreshold, maxPourAngle, currentPourAngle);
        float actualPourRate = pourRate * angleRatio;
        
        // Chỉ giảm lượng nước nếu có targetGlass và Glass chấp nhận nước
        if (targetGlass != null && !targetGlass.IsFull())
        {
            // Kiểm tra xem cup đã connect với glass khác chưa
            if (connectedGlass != null && connectedGlass != targetGlass)
            {
                if (debugMode)
                    Debug.Log($"[PouringCup] Cannot pour - already connected to {connectedGlass.name}, cannot pour to {targetGlass.name}");
                return; // Không cho đổ vào glass khác
            }
            
            float previousGlassAmount = targetGlass.GetCurrentAmount();
            
            // Tạo WaterData để test
            WaterData waterData = new WaterData(waterGroupName, liquidColor, waterTexture30, waterTexture80);
            
            // Thử thêm nước vào glass
            targetGlass.AddWater(actualPourRate * Time.deltaTime, waterData);
            
            // Chỉ trừ nước từ cup nếu glass thực sự nhận được nước
            float actualAdded = targetGlass.GetCurrentAmount() - previousGlassAmount;
            if (actualAdded > 0)
            {
                currentAmount = Mathf.Max(0, currentAmount - actualAdded);
                UpdateLiquidVisual();
                
                // Set connectedGlass sau khi thêm thành công waterData lần đầu
                if (connectedGlass == null)
                {
                    connectedGlass = targetGlass;
                    if (debugMode)
                        Debug.Log($"[PouringCup] Connected to glass {targetGlass.name} after first successful water transfer");
                }
            }
        }
    }
    
    /// <summary>
    /// Phát hiện glass gần đó bằng raycast từ pourPoint xuống
    /// </summary>
    private void DetectGlass()
    {
        if (pourPoint == null) return;
        
        // Raycast từ pourPoint thẳng xuống
        Vector3 rayOrigin = pourPoint.position;
        Vector3 rayDirection = Vector3.down;
        
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, detectionDistance);
        
        GlassController closestGlass = null;
        float closestDistance = detectionDistance;
        
        foreach (RaycastHit hit in hits)
        {
            GlassController glass = hit.collider.GetComponent<GlassController>();
            if (glass != null && !glass.IsFull())
            {
                float distance = hit.distance;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestGlass = glass;
                }
            }
        }
        
        // Cập nhật target glass
        if (closestGlass != null && closestGlass != targetGlass)
        {
            targetGlass = closestGlass;
            if (debugMode)
                Debug.Log($"[PouringCup] Detected glass at distance: {closestDistance:F2}m (via raycast)");
        }
        else if (closestGlass == null && targetGlass != null)
        {
            targetGlass = null;
        }
        
        // Thêm nước vào glass nếu có (logic này đã chuyển vào PourWater)
        // DetectGlass chỉ để tìm targetGlass
    }
    
    /// <summary>
    /// Cập nhật visual stream - dòng nước chảy từ mũi cốc xuống
    /// Dựa trên công thức từ WaterStream với gravity effect
    /// </summary>
    private void UpdatePourStream()
    {
        if (pourPoint == null || currentAmount <= 0)
        {
            pourStream.enabled = false;
            return;
        }
        
        pourStream.enabled = true;
        
        Vector3 startPoint = pourPoint.position;
        float effectiveLength = streamLength;
        
        // Vẽ dòng nước từ pour point xuống với gravity effect
        // Công thức giống WaterStream
        for (int i = 0; i < pourStream.positionCount; i++)
        {
            float t = i / (float)(pourStream.positionCount - 1);
            float distance = effectiveLength * t;
            float time = distance / waterSpeed;
            // Gravity drop: y = 0.5 * g * t²
            float dropDistance = 0.5f * 9.81f * time * time;
            
            Vector3 point = startPoint + Vector3.down * distance;
            point.y -= dropDistance * 0.1f; // Scale down để không quá lớn
            
            pourStream.SetPosition(i, point);
        }
    }
    
    private void UpdateLiquidVisual()
    {
        if (liquidObject == null || liquidRenderer == null) return;
        
        liquidObject.SetActive(currentAmount > 0);
        
        if (currentAmount <= 0) return;
        
        float ratio = currentAmount / maxCapacity;
        
        // Update scale
        float scaleY = ratio;
        Vector3 newScale = new Vector3(1, 1 * scaleY, 1f);
        liquidObject.transform.localScale = newScale;
        
        // Update color
        Color liquidColor = Color.Lerp(emptyColor, fullColor, ratio);
        liquidRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", liquidColor);
        liquidRenderer.SetPropertyBlock(propBlock);
    }
    
    /// <summary>
    /// Lưu vị trí và rotation ban đầu
    /// </summary>
    private void SaveInitialPosition()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        hasInitialPositionSaved = true;
        
        if (debugMode)
            Debug.Log($"[PouringCup] Saved initial position: {initialPosition}, rotation: {initialRotation.eulerAngles}");
    }
    
    /// <summary>
    /// Khôi phục về vị trí và rotation ban đầu
    /// </summary>
    public void RestartPosition()
    {
        if (!hasInitialPositionSaved)
        {
            if (debugMode)
                Debug.LogWarning("[PouringCup] Cannot restart position - initial position not saved yet!");
            return;
        }
        
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        // Reset velocity nếu có rigidbody
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        if (debugMode)
            Debug.Log($"[PouringCup] Position restarted to: {initialPosition}, rotation: {initialRotation.eulerAngles}");
    }
    
    // Setters
    public void AddWater(float amount)
    {
        currentAmount = Mathf.Min(currentAmount + amount, maxCapacity);
        UpdateLiquidVisual();
    }
    
    public void SetAmount(float amount)
    {
        currentAmount = Mathf.Clamp(amount, 0f, maxCapacity);
        UpdateLiquidVisual();
    }
    
    public void EmptyCup()
    {
        currentAmount = 0f;
        
        // Clear connection when empty
        if (connectedGlass != null)
        {
            connectedGlass = null;
            if (debugMode)
                Debug.Log("[PouringCup] Cleared glass connection - cup emptied");
        }
        
        UpdateLiquidVisual();
    }
    
    /// <summary>
    /// Reset connection để có thể đổ vào glass khác
    /// </summary>
    public void ClearConnection()
    {
        if (connectedGlass != null)
        {
            connectedGlass = null;
            if (debugMode)
                Debug.Log("[PouringCup] Connection cleared manually");
        }
    }
    
    // Getters
    public bool IsFull() => currentAmount >= maxCapacity;
    public bool IsEmpty() => currentAmount <= 0;
    public bool IsPouring() => isPouring;
    public float GetCurrentAmount() => currentAmount;
    public float GetMaxCapacity() => maxCapacity;
    public float GetFillPercentage() => currentAmount / maxCapacity;
    public float GetCurrentPourAngle() => currentPourAngle;
    public Vector3 GetPourPointPosition() => pourPoint != null ? pourPoint.position : transform.position;
    public GlassController GetConnectedGlass() => connectedGlass;
    
    // Gizmos để debug
    private void OnDrawGizmos()
    {
        if (!debugMode) return;
        
        if (pourPoint != null)
        {
            // Vẽ điểm đổ
            Gizmos.color = isPouring ? Color.cyan : Color.gray;
            Gizmos.DrawWireSphere(pourPoint.position, 0.05f);
            
            // Vẽ dòng nước
            if (isPouring && currentAmount > 0)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(pourPoint.position, Vector3.down * streamLength);
            }
        }
        
        // Vẽ detection ray
        if (pourPoint != null)
        {
            Gizmos.color = new Color(1, 1, 0, 0.7f);
            Gizmos.DrawRay(pourPoint.position, Vector3.down * detectionDistance);
        }
        
        // Vẽ góc nghiêng
        if (Application.isPlaying)
        {
            Gizmos.color = (currentPourAngle >= pourAngleThreshold && currentPourAngle <= maxPourAngle) 
                ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position, transform.up * 0.5f);
        }
    }
}