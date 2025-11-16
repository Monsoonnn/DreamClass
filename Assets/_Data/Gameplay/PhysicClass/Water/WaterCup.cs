using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(BoxCollider))]
public class WaterCup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider catchArea;
    [SerializeField] private GameObject liquidObject;
    [SerializeField] private WaterStream waterStream;
    
    [Header("Cup Settings")]
    [SerializeField] private float maxCapacity = 100f;
    [SerializeField] private float currentAmount = 0f;
    [SerializeField] private float fillRate = 20f; // Lượng nước tăng mỗi giây
    
    [Header("Visual Settings")]
    [SerializeField] private Vector3 liquidBaseScale = new Vector3(0.9f, 1f, 0.9f);
    [SerializeField] private Vector3 liquidBasePosition = Vector3.zero;
    [SerializeField] private Color emptyColor = new Color(0.3f, 0.6f, 1f, 0.3f);
    [SerializeField] private Color fullColor = new Color(0.2f, 0.5f, 1f, 0.8f);
    
    [Header("UI")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Slider fillSlider;
    [SerializeField] private Text percentageText;
    
    private bool isUnderWaterStream = false;
    private Renderer liquidRenderer;
    private MaterialPropertyBlock propBlock;
    
    private const float POSITION_FACTOR = 1.5f;
    
    void Start()
    {
        SetupComponents();
        UpdateLiquidVisual();
    }
    
    void Update()
    {
        CheckWaterStreamPosition();
        
        if (isUnderWaterStream && waterStream != null && waterStream.IsFlowing())
        {
            FillCup(fillRate * Time.deltaTime);
        }
        
        UpdateUI();
    }
    
    private void SetupComponents()
    {
        // Setup catch area
        if (catchArea == null)
        {
            catchArea = GetComponent<BoxCollider>();
        }
        catchArea.isTrigger = true;
        
        // Setup liquid object
        if (liquidObject == null)
        {
            liquidObject = transform.Find("Liquid")?.gameObject;
            if (liquidObject == null)
            {
                liquidObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                liquidObject.name = "Liquid";
                liquidObject.transform.SetParent(transform);
                liquidObject.transform.localPosition = liquidBasePosition;
                liquidObject.transform.localScale = liquidBaseScale;
                
                // Xóa collider của liquid
                Destroy(liquidObject.GetComponent<Collider>());
            }
        }
        
        liquidRenderer = liquidObject.GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        
        // Setup UI
        if (uiCanvas != null)
        {
            if (fillSlider == null)
                fillSlider = uiCanvas.GetComponentInChildren<Slider>();
            if (percentageText == null)
                percentageText = uiCanvas.GetComponentInChildren<Text>();
        }
        
        // Find water stream if not assigned
        if (waterStream == null)
        {
            waterStream = FindObjectOfType<WaterStream>();
        }
    }
    
    private void CheckWaterStreamPosition()
    {
        if (waterStream == null) return;
        
        Vector3 streamEndPoint = waterStream.GetStreamEndPoint();
        Bounds bounds = catchArea.bounds;
        
        // Kiểm tra xem điểm cuối của dòng nước có nằm trong vùng hứng nước không
        isUnderWaterStream = bounds.Contains(streamEndPoint);
    }
    
    public void FillCup(float amount)
    {
        if (currentAmount >= maxCapacity) return;
        
        currentAmount = Mathf.Min(currentAmount + amount, maxCapacity);
        UpdateLiquidVisual();
    }
    
    public void EmptyCup()
    {
        currentAmount = 0f;
        UpdateLiquidVisual();
    }
    
    public void SetAmount(float amount)
    {
        currentAmount = Mathf.Clamp(amount, 0f, maxCapacity);
        UpdateLiquidVisual();
    }
    
    private void UpdateLiquidVisual()
    {
        if (liquidObject == null || liquidRenderer == null) return;
        
        // Show/hide liquid
        liquidObject.SetActive(currentAmount > 0);
        
        if (currentAmount <= 0) return;
        
        // Calculate fill ratio
        float ratio = currentAmount / maxCapacity;
        
        // Update scale
        float scaleY = ratio;
        Vector3 newScale = new Vector3(
            liquidBaseScale.x,
            liquidBaseScale.y * scaleY,
            liquidBaseScale.z
        );
        liquidObject.transform.localScale = newScale;
        
        // Update position (nước dâng lên từ đáy)
        float offsetY = (1 - scaleY) * POSITION_FACTOR;
        Vector3 newPosition = new Vector3(
            liquidBasePosition.x,
            liquidBasePosition.y + offsetY,
            liquidBasePosition.z
        );
        liquidObject.transform.localPosition = newPosition;
        
        // Update color
        Color liquidColor = Color.Lerp(emptyColor, fullColor, ratio);
        liquidRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", liquidColor);
        liquidRenderer.SetPropertyBlock(propBlock);
    }
    
    private void UpdateUI()
    {
        if (uiCanvas == null) return;
        
        float ratio = currentAmount / maxCapacity;
        
        if (fillSlider != null)
        {
            fillSlider.value = ratio;
        }
        
        if (percentageText != null)
        {
            percentageText.text = $"{Mathf.RoundToInt(ratio * 100)}%";
        }
    }
    
    public float GetFillPercentage()
    {
        return currentAmount / maxCapacity;
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
    
    // Visualize catch area
    private void OnDrawGizmos()
    {
        if (catchArea != null)
        {
            Gizmos.color = isUnderWaterStream ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(catchArea.bounds.center, catchArea.bounds.size);
        }
    }
}