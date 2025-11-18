using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Oculus.Interaction.HandGrab;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ForceHand))]
public class WaterCup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider catchArea;
    [SerializeField] private GameObject liquidObject;
    [SerializeField] private WaterStream waterStream;
    [SerializeField] private Rigidbody rb;
    public ForceHand forceHand;
    
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
    [SerializeField] private GameObject loadingText;
    [SerializeField] private GameObject successText;
    [SerializeField] private GameObject fullText;
    
    [Header("Guide Integration")]
    private bool isHaveWater = false;
    private bool wasFillingLastFrame = false;
    
    private bool isUnderWaterStream = false;
    private bool isCatchingWater = false; // Cup is actively catching water
    private Renderer liquidRenderer;
    private MaterialPropertyBlock propBlock;
    
    private const float POSITION_FACTOR = 1.5f;
    private const float WATER_THRESHOLD = 10f; // Minimum water to consider "has water"
    
    private Vector3 basePositionOrigin;
    private Vector3 baseScaleOrigin;
    private Quaternion baseRotationOrigin;
    
    void Start()
    {
        SetupComponents();
        LoadBaseTransform();
        UpdateLiquidVisual();
    }
    
    private void LoadBaseTransform()
    {
        basePositionOrigin = this.transform.position;
        baseScaleOrigin = this.transform.localScale;
        baseRotationOrigin = this.transform.rotation;
    }
    
    void Update()
    {
        CheckWaterStreamIntersection();
        
        bool isCurrentlyFilling = isCatchingWater && waterStream != null && waterStream.IsFlowing();
        
        // Update UI state based on filling status
        if (isCurrentlyFilling)
        {
            if (!wasFillingLastFrame)
            {
                // Just started filling
                ShowUIState(loading: true);
            }
            
            // Check if full
            if (IsFull())
            {
                ShowUIState(full: true);
                // Auto stop water when cup is full
                if (waterStream != null)
                {
                    waterStream.StopFlow();
                    Debug.Log("[WaterCup] Cup is full, stopping water stream");
                }
            }
            else
            {
                // Continue filling
                FillCup(fillRate * Time.deltaTime);
            }
        }
        else if (wasFillingLastFrame)
        {
            // Just stopped filling
            if (IsFull())
            {
                ShowUIState(success: true);
                StartCoroutine(HideSuccessTextAfterDelay(2f));
                // Complete step when full
                if (!isHaveWater)
                {
                    GuideStepManager.Instance.CompleteStep("GET_WATER");
                    isHaveWater = true;
                    Debug.Log("[WaterCup] GET_WATER step completed! (Cup full)");
                }
            }
            else if (currentAmount >= WATER_THRESHOLD)
            {
                // Has water but not full - cup left the stream
                ShowUIState(success: true);
                StartCoroutine(HideSuccessTextAfterDelay(2f));
                
                // Auto stop water when cup leaves with water
                if (waterStream != null && waterStream.IsFlowing())
                {
                    waterStream.StopFlow();
                    Debug.Log("[WaterCup] Cup left stream with water, stopping water stream");
                }
                
                // Complete step when has water and left stream
                if (!isHaveWater)
                {
                    GuideStepManager.Instance.CompleteStep("GET_WATER");
                    isHaveWater = true;
                    Debug.Log("[WaterCup] GET_WATER step completed! (Cup left with water)");
                }
            }
            else
            {
                HideAllText();
            }
        }
        
        wasFillingLastFrame = isCurrentlyFilling;
        UpdateUI();
    }
    
    private void SetupComponents()
    {
        // Setup catch area
        if (catchArea == null)
        {
            catchArea = GetComponent<BoxCollider>();
        }
        
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
                
            // Try to find UI text objects
            if (loadingText == null)
            {
                Transform loadingTrans = uiCanvas.transform.Find("LoadingText");
                if (loadingTrans != null) loadingText = loadingTrans.gameObject;
            }
            if (successText == null)
            {
                Transform successTrans = uiCanvas.transform.Find("SuccessText");
                if (successTrans != null) successText = successTrans.gameObject;
            }
            if (fullText == null)
            {
                Transform fullTrans = uiCanvas.transform.Find("FullText");
                if (fullTrans != null) fullText = fullTrans.gameObject;
            }
        }
        
        // Find water stream if not assigned
        if (waterStream == null)
        {
            waterStream = Object.FindFirstObjectByType<WaterStream>();
        }
    }
    
    private void CheckWaterStreamIntersection()
    {
        if (waterStream == null || !waterStream.IsFlowing()) 
        {
            if (isCatchingWater)
            {
                // Stop catching water
                isCatchingWater = false;
                if (waterStream != null)
                    waterStream.SetCatchingCup(null);
            }
            return;
        }
        
        // Check if water stream intersects with catch area
        bool streamIntersects = IsStreamIntersectingCup();
        
        if (streamIntersects && !IsFull())
        {
            if (!isCatchingWater)
            {
                // Start catching water
                isCatchingWater = true;
                waterStream.SetCatchingCup(this);
                Debug.Log("[WaterCup] Started catching water");
            }
            
            // Get water surface position in the cup
            Vector3 waterSurfacePos = GetWaterSurfacePosition();
            
            // Update stream endpoint to water surface (only Y distance matters)
            waterStream.SetStreamEndPoint(waterSurfacePos);
        }
        else if (isCatchingWater)
        {
            // Only stop catching if currently catching and (not intersecting OR cup is full)
            // Check if cup is full first before calling SetCatchingCup(null)
            if (IsFull())
            {
                waterStream.SetCatchingCup(this); 
                // Complete GET_WATER step when cup is full
                if (!isHaveWater && currentAmount >= WATER_THRESHOLD)
                {
                    GuideStepManager.Instance.CompleteStep("GET_WATER");
                    isHaveWater = true;
                    Debug.Log("[WaterCup] GET_WATER step completed!");
                }
            }
            else
            {
                // Stop catching water
                isCatchingWater = false;
                waterStream.SetCatchingCup(null);
                Debug.Log("[WaterCup] Stopped catching water");
                
                // Complete GET_WATER step when cup leaves stream with enough water
                if (!isHaveWater && currentAmount >= WATER_THRESHOLD)
                {
                    GuideStepManager.Instance.CompleteStep("GET_WATER");
                    isHaveWater = true;
                    Debug.Log("[WaterCup] GET_WATER step completed!");
                }
            }
        }
    }
    
    private bool IsStreamIntersectingCup()
    {
        if (waterStream == null) return false;
        
        Bounds bounds = catchArea.bounds;
        Vector3 streamSource = waterStream.GetStreamStartPoint();
        Vector3 streamDirection = Vector3.down;
        
        // Check if downward ray from stream source intersects with catch area
        Ray ray = new Ray(streamSource, streamDirection);
        return bounds.IntersectRay(ray);
    }
    
    public Vector3 GetWaterSurfacePosition()
    {
        if (liquidObject == null || currentAmount <= 0)
        {
            // Return bottom of catch area if no water
            return catchArea.bounds.center - Vector3.up * (catchArea.bounds.size.y * 0.5f);
        }
        
        // Calculate water surface position based on fill level
        float ratio = currentAmount / maxCapacity;
        float waterHeight = catchArea.bounds.size.y * ratio;
        
        Vector3 bottomPos = catchArea.bounds.center - Vector3.up * (catchArea.bounds.size.y * 0.5f);
        return bottomPos + Vector3.up * waterHeight;
    }
    
    public void FillCup(float amount)
    {
        if (currentAmount >= maxCapacity) return;
        
        float previousAmount = currentAmount;
        currentAmount = Mathf.Min(currentAmount + amount, maxCapacity);
        UpdateLiquidVisual();
        
        // Update stream endpoint as water rises
        if (isCatchingWater && waterStream != null)
        {
            waterStream.SetStreamEndPoint(GetWaterSurfacePosition());
        }
    }
    
    public void EmptyCup()
    {
        currentAmount = 0f;
        isHaveWater = false;
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
    
    private void ShowUIState(bool loading = false, bool success = false, bool full = false)
    {
        // if (uiCanvas != null) uiCanvas.gameObject.SetActive(true);
        // if (loadingText != null) loadingText.SetActive(loading);
        // if (successText != null) successText.SetActive(success);
        // if (fullText != null) fullText.SetActive(full);
    }
    
    private void HideAllText()
    {
        if (uiCanvas != null) uiCanvas.gameObject.SetActive(false);
        if (loadingText != null) loadingText.SetActive(false);
        if (successText != null) successText.SetActive(false);
        if (fullText != null) fullText.SetActive(false);
    }
    
    public void HideAllUI()
    {
        HideAllText();
    }
    
    private IEnumerator HideSuccessTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideAllText();
    }
    
    
    public void SetIsHaveWater(bool value)
    {
        this.isHaveWater = value;
    }
    
    public bool GetIsHaveWater()
    {
        return isHaveWater;
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
    
    public bool IsCatchingWater()
    {
        return isCatchingWater;
    }
    
    public void ResetPosition()
    {
        this.transform.position = basePositionOrigin;
        this.transform.localScale = baseScaleOrigin;
        this.transform.rotation = baseRotationOrigin;
        this.gameObject.SetActive(true);
    }
    
    public float GetMaxLiquid()
    {
        return maxCapacity;
    }
    
    public float GetCurrentLiquid()
    {
        return currentAmount;
    }
    
    // Visualize catch area and water surface
    private void OnDrawGizmos()
    {
        if (catchArea != null)
        {
            Gizmos.color = isCatchingWater ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(catchArea.bounds.center, catchArea.bounds.size);
            
            // Draw water surface
            if (currentAmount > 0)
            {
                Vector3 waterSurface = GetWaterSurfacePosition();
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(waterSurface, 0.05f);
            }
        }
    }
}