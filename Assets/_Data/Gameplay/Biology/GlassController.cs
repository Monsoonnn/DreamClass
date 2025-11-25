using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Unity.VisualScripting;

/// <summary>
/// Cốc nhận nước từ PouringCup
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class GlassController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider catchArea;
    [SerializeField] private GameObject liquidObject;
    [SerializeField] private Rigidbody rb;
    public GameObject modelSnapVisual;

    public GameObject ISKD_RayGrabInteraction;
    public GameObject ISDK_HandGrabInteraction;
    
    [Header("Glass Settings")]
    [SerializeField] private float maxCapacity = 100f;
    [SerializeField] private float currentAmount = 0f;
    [SerializeField] private float fillSpeed = 1f; // Tốc độ tăng visual (smooth animation)
    
    [Header("Visual Settings")]
    [SerializeField] private Color emptyColor = new Color(0.3f, 0.6f, 1f, 0.3f);
    [SerializeField] private Color fullColor = new Color(0.2f, 0.5f, 1f, 0.8f);
    

    [Header("Runtime Variables")]
    public FlowerController connectedFlower = null;

    private Renderer liquidRenderer;
    private MaterialPropertyBlock propBlock;
    private float visualAmount = 0f; // Smooth visual representation
    private bool isReceivingWater = false;
    private bool isFlowerNearby = false;
    
    private WaterData currentWaterData = null; // Data nước từ Cup
    
    private const float SPLASH_THRESHOLD = 5f; // Tốc độ đổ để tạo splash effect
    
    void Start()
    {
        SetupComponents();
        UpdateLiquidVisual();
    }
    
    void Update()
    {
        // Smooth animation cho liquid level
        if (visualAmount != currentAmount)
        {
            float targetAmount = currentAmount;
            
            // Khi có flower, visualAmount không được giảm dưới 30f
            if (connectedFlower != null)
            {
                targetAmount = Mathf.Max(currentAmount, 100f);
            }
            
            visualAmount = Mathf.Lerp(visualAmount, targetAmount, fillSpeed * Time.deltaTime);
            UpdateLiquidVisual();
        }
    }
    
    private void SetupComponents()
    {
        // Setup catch area
        if (catchArea == null)
        {
            catchArea = GetComponent<BoxCollider>();
        }
        catchArea.isTrigger = true; // Đảm bảo là trigger để detect water stream
        
        // Setup liquid object
        if (liquidObject == null)
        {
            liquidObject = transform.Find("Liquid")?.gameObject;
            if (liquidObject == null)
            {
                liquidObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                liquidObject.name = "Liquid";
                liquidObject.transform.SetParent(transform);
                liquidObject.transform.localPosition = Vector3.zero;
                liquidObject.transform.localScale = Vector3.one;
                Destroy(liquidObject.GetComponent<Collider>());
            }
        }
        
        liquidRenderer = liquidObject.GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        
        // Setup rigidbody
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        
        // Setup snap visual
        if (modelSnapVisual == null)
        {
            Debug.LogWarning("[GlassController] Snap visual not assigned!");
        }
    }
    
    /// <summary>
    /// Thêm nước vào glass
    /// </summary>
    private bool isHavedWaterData = false;
    public bool IsHavedWaterData => isHavedWaterData;

    public void AddWater(float amount, WaterData waterData = null)
    {
        if (currentAmount >= maxCapacity) return;
        
        // Chỉ nhận nước nếu:
        // 1. Glass chưa có waterData (empty)
        // 2. WaterData giống với data hiện tại (so sánh groupName)
        if (isHavedWaterData && waterData != null && waterData.groupName != currentWaterData.groupName) return;

        float previousAmount = currentAmount;
        currentAmount = Mathf.Min(currentAmount + amount, maxCapacity);
        
        // Lưu water data nếu có và chưa có data
        if (waterData != null && !isHavedWaterData)
        {
            currentWaterData = waterData;
            liquidRenderer.material = waterData.liquidColor;
            isHavedWaterData = true;
        }
    }
    
    /// <summary>
    /// Set trực tiếp lượng nước
    /// </summary>
    public void SetAmount(float amount)
    {
        currentAmount = Mathf.Clamp(amount, 0f, maxCapacity);
        visualAmount = currentAmount; // Instant update
        UpdateLiquidVisual();
    }

    public void SetAmount(float amount, float minCapacity)
    {
        currentAmount = Mathf.Clamp(amount, minCapacity, maxCapacity);
        
        // Khi có flower, visualAmount không được giảm dưới 30f
        if (connectedFlower != null)
        {
            visualAmount = Mathf.Max(currentAmount, 100f);
        }
        else
        {
            visualAmount = currentAmount; // Instant update
        }
        
        UpdateLiquidVisual();
    }
    
    /// <summary>
    /// Đổ hết nước ra
    /// </summary>
    public void EmptyGlass()
    {
        currentAmount = 0f;
        visualAmount = 0f;
        
        // Reset water data state
        currentWaterData = null;
        isHavedWaterData = false;
        
        UpdateLiquidVisual();
    }
    
    private void UpdateLiquidVisual()
    {
        if (liquidObject == null || liquidRenderer == null) return;
        
        // Show/hide liquid
        liquidObject.SetActive(visualAmount > 0);
        
        if (visualAmount <= 0) return;
        
        // Calculate fill ratio
        float ratio = visualAmount / maxCapacity;
        
        // Update scale - chỉ scale theo tỷ lệ %
        Vector3 newScale = Vector3.one * ratio;
        liquidObject.transform.localScale = new Vector3(1, newScale.y, 1);
        
        // // Update color
        // Color liquidColor = Color.Lerp(emptyColor, fullColor, ratio);
        // liquidRenderer.GetPropertyBlock(propBlock);
        // propBlock.SetColor("_Color", liquidColor);
        // liquidRenderer.SetPropertyBlock(propBlock);
    }
    
    
    /// <summary>
    /// Lấy vị trí mặt nước hiện tại
    /// </summary>
    public Vector3 GetWaterSurfacePosition()
    {
        if (liquidObject == null || currentAmount <= 0)
        {
            return catchArea.bounds.center - Vector3.up * (catchArea.bounds.size.y * 0.5f);
        }
        
        float ratio = currentAmount / maxCapacity;
        float waterHeight = catchArea.bounds.size.y * ratio;
        Vector3 bottomPos = catchArea.bounds.center - Vector3.up * (catchArea.bounds.size.y * 0.5f);
        return bottomPos + Vector3.up * waterHeight;
    }
    
    public bool IsFull()
    {
        return currentAmount >= maxCapacity;
    }
    
    public bool IsEmpty()
    {
        return currentAmount <= 0;
    }
    
    public float GetCurrentAmount()
    {
        return currentAmount;
    }
    
    public float GetMaxCapacity()
    {
        return maxCapacity;
    }
    
    public float GetFillPercentage()
    {
        return currentAmount / maxCapacity;
    }
    
    /// <summary>
    /// Lấy Water Data từ Glass
    /// </summary>
    public WaterData GetWaterData()
    {
        return currentWaterData;
    }
    
    /// <summary>
    /// Cập nhật trạng thái có Flower gần đó
    /// </summary>
    public void SetFlowerNearby(bool nearby)
    {
        isFlowerNearby = nearby;
        if (modelSnapVisual != null)
        {
            modelSnapVisual.SetActive(nearby && !IsEmpty());
        }
    }
    
    /// <summary>
    /// Lấy vị trí của snap visual trên Glass
    /// </summary>
    public Vector3 GetSnapVisualPosition()
    {
        if (modelSnapVisual != null)
            return modelSnapVisual.transform.position;
        return GetWaterSurfacePosition();
    }
    
    public bool IsFlowerNearby()
    {
        return isFlowerNearby;
    }
    
    /// <summary>
    /// Ẩn snap visual (được gọi khi Flower snap vào)
    /// </summary>
    public void HideSnapVisual()
    {
        if (modelSnapVisual != null)
        {
            modelSnapVisual.SetActive(false);
        }
    }
    
    /// <summary>
    /// Hiện snap visual
    /// </summary>
    public void ShowSnapVisual()
    {
        if (modelSnapVisual != null)
        {
            modelSnapVisual.SetActive(isFlowerNearby && !IsEmpty());
        }
    }

    public void EnableInteractions()
    {
        if (ISKD_RayGrabInteraction != null)
            ISKD_RayGrabInteraction.SetActive(true);
        if (ISDK_HandGrabInteraction != null)
            ISDK_HandGrabInteraction.SetActive(true);
    }

    public void DisableInteractions()
    {
        if (ISKD_RayGrabInteraction != null)
            ISKD_RayGrabInteraction.SetActive(false);
        if (ISDK_HandGrabInteraction != null)
            ISDK_HandGrabInteraction.SetActive(false);
    }
    
    // Visualize catch area
    private void OnDrawGizmos()
    {
        if (catchArea != null)
        {
            Gizmos.color = IsFull() ? Color.red : Color.green;
            Gizmos.DrawWireCube(catchArea.bounds.center, catchArea.bounds.size);
            
            // Draw water surface
            if (currentAmount > 0)
            {
                Vector3 waterSurface = GetWaterSurfacePosition();
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(waterSurface, 0.05f);
                
                // Draw water plane
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Vector3 size = catchArea.bounds.size;
                size.y = 0.01f;
                Gizmos.DrawCube(waterSurface, size);
            }
        }
    }
}