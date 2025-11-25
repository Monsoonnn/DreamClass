using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class FlowerController : NewMonobehavior
{
    [Header("Debug")]
    [SerializeField] private bool debugPhysics = false;
    
    [Header("References")]
    [SerializeField] private Renderer modelRenderer; // Renderer của model Flower
    [SerializeField] private Texture2D dryTexture; // Texture khô ban đầu

    [Header("Snap Settings")]
    public SnapTrigger snapTrigger;
    
    [Header("Initial Position")]
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool hasInitialPositionSaved = false;
    
    private Rigidbody rb;
    private Collider flowerCollider;
    private GlassController connectedGlass = null;
    [Header("Absorption Settings")]
    public float absorptionTimer = 0f;
    public float waterAbsorptionPercentage = 0f; // % nước hấp thụ (0-100)
    
    private WaterTransportMaterialHandler materialHandler;
    
    private float absorptionDuration = 5f; // Thời gian hấp thụ (giây) - tùy lượng nước
    private const float MIN_ABSORPTION_DURATION = 2f; // Tối thiểu (nước ít)
    private const float MAX_ABSORPTION_DURATION = 60f; // Tối đa (nước nhiều)
    private bool isAbsorbing = false; // Đang trong quá trình hấp thụ


    protected override void LoadComponents()
    {
        base.LoadComponents();
        this.LoadSnapTrigger();
    }

    private void LoadSnapTrigger()
    {
        if(this.snapTrigger != null) return;
        this.snapTrigger = GetComponentInChildren<SnapTrigger>();
    }


    protected override void Start()
    {
        base.Start();
        
        // Lưu vị trí và rotation ban đầu
        SaveInitialPosition();
        
        rb = GetComponent<Rigidbody>();
        flowerCollider = GetComponent<Collider>();
        
        // Tìm Renderer nếu chưa assign
        if (modelRenderer == null)
        {
            modelRenderer = GetComponent<Renderer>();
        }
        
        // Setup Material Handler
        SetupMaterialHandler();
    }
    
    void Update()
    {
        // Nếu đang hấp thụ, tăng timer
        if (isAbsorbing)
        {
            absorptionTimer += Time.deltaTime;
            
            // Nếu đủ thời gian hoặc glass hết nước hoặc đã hấp thụ đủ (>= 80%) thì dừng
            if (absorptionTimer >= absorptionDuration || 
                (connectedGlass != null && connectedGlass.IsEmpty()) ||
                (connectedGlass != null && waterAbsorptionPercentage >= 80f))
            {
                isAbsorbing = false;
                if (debugPhysics) Debug.Log("[FlowerController] Absorption complete", this);
            }
            else
            {
                // Cập nhật absorption dần dần
                UpdateAbsorptionProgress();
            }
        }
    }
    
    /// <summary>
    /// Setup Material Handler với materials từ Renderer
    /// </summary>
    private void SetupMaterialHandler()
    {
        if (modelRenderer == null)
        {
            if (debugPhysics) Debug.LogWarning("[FlowerController] No renderer found!", this);
            return;
        }
        
        materialHandler = gameObject.AddComponent<WaterTransportMaterialHandler>();
        
        // Lấy materials từ renderer (clone để không ảnh hưởng shared material)
        Material[] mats = modelRenderer.materials;
        List<Material> matList = new List<Material>(mats);
        
        // Initialize với materials (textures sẽ được thêm khi snap)
        materialHandler.Initialize(matList, new List<Texture2D>());
        
        if (debugPhysics) Debug.Log($"[FlowerController] Material handler initialized with {matList.Count} materials", this);
    }
    
    /// <summary>
    /// Tắt physics khi snap vào Glass
    /// </summary>
    public void DisablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            
            if (debugPhysics) Debug.Log("[FlowerController] Physics disabled", this);
        }
    }
    
    /// <summary>
    /// Bật physics khi unsnap
    /// </summary>
    public void EnablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            
            if (debugPhysics) Debug.Log("[FlowerController] Physics enabled", this);
        }
    }
    
    /// <summary>
    /// Được gọi khi snap vào Glass thành công
    /// </summary>
    public void OnSnapped(GlassController glass)
    {
        connectedGlass = glass;
        if (debugPhysics) Debug.Log("[FlowerController] Snapped to Glass", this);
        
        // Simulate water absorption
        SimulateWaterAbsorption();
    }
    
    /// <summary>
    /// Simulation: Cây hấp thụ nước từ Glass
    /// </summary>
    private void SimulateWaterAbsorption()
    {
        if (connectedGlass == null) return;
        
        WaterData waterData = connectedGlass.GetWaterData();
        float fillPercentage = connectedGlass.GetFillPercentage();
        
        if (waterData == null)
        {
            if (debugPhysics) Debug.Log("[FlowerController] No water data in glass", this);
            return;
        }
        
        // Chỉ bắt đầu absorption nếu glass có nước
        if (fillPercentage > 0f)
        {
            // Tính thời gian hấp thụ dựa trên lượng nước (0-100% → 2f-45f giây)
            absorptionDuration = Mathf.Lerp(MIN_ABSORPTION_DURATION, MAX_ABSORPTION_DURATION, fillPercentage);
            absorptionTimer = 0f;
            isAbsorbing = true;
            
            if (debugPhysics) 
                Debug.Log($"[FlowerController] Started absorption from {waterData.groupName} (Water: {fillPercentage * 100f:F1}% | Duration: {absorptionDuration:F1}s)", this);
        }
    }
    
    /// <summary>
    /// Cập nhật tiến trình hấp thụ theo time
    /// </summary>
    private void UpdateAbsorptionProgress()
    {
        if (connectedGlass == null) return;
        
        WaterData waterData = connectedGlass.GetWaterData();
        float currentAmount = connectedGlass.GetCurrentAmount();
        // Tính % hấp thụ dựa trên thời gian
        float absorptionProgress = Mathf.Clamp01(absorptionTimer / absorptionDuration);
        waterAbsorptionPercentage = absorptionProgress * 100f;
        
        // Trừ nước từ glass theo % hấp thụ
        float remainingWater = currentAmount * (1f - absorptionProgress);
        connectedGlass.SetAmount(remainingWater, 100f);
        
        if (debugPhysics && (int)(waterAbsorptionPercentage) % 20 == 0)
        {
            Debug.Log($"[FlowerController] Absorption: {waterAbsorptionPercentage:F1}% | Remaining water: {remainingWater * 100f:F1}%", this);
        }
        
        // Cập nhật texture
        UpdateFlowerTexture(waterData, waterAbsorptionPercentage);
    }
    
    /// <summary>
    /// Được gọi khi unsnap khỏi Glass
    /// </summary>
    public void OnUnsnapped()
    {
        if (connectedGlass != null)
        {
            connectedGlass.connectedFlower = null;
        }
        connectedGlass = null;
        
        // Reset absorption state
        isAbsorbing = false;
        absorptionTimer = 0f;
        waterAbsorptionPercentage = 0f;
        
        if (debugPhysics) Debug.Log("[FlowerController] Unsnapped from Glass", this);
    }
    
    public GlassController GetConnectedGlass()
    {
        return connectedGlass;
    }
    
    public float GetWaterAbsorptionPercentage()
    {
        return waterAbsorptionPercentage;
    }

    public void RestartDryTexture(){
        if(materialHandler == null) return;
        materialHandler.ChangeBaseMap(dryTexture);
    }
    
    /// <summary>
    /// Cập nhật texture của Flower dựa trên lượng nước hấp thụ
    /// </summary>
    private void UpdateFlowerTexture(WaterData waterData, float absorptionPercentage)
    {
        if (materialHandler == null || waterData == null) return;
        
        Texture2D targetTexture = null;
        
        // Nếu nước < 30% thì giữ nguyên texture, không update
        if (absorptionPercentage < 30f)
        {
            if (debugPhysics) 
                Debug.Log($"[FlowerController] Water absorption below 30%, keeping texture (Absorption: {absorptionPercentage:F1}%)", this);
            return;
        }

        // Chọn texture dựa trên % absorption
        if (absorptionPercentage >= 30f && absorptionPercentage < 80f)
        {
            targetTexture = waterData.texture30;
        }
        else if (absorptionPercentage >= 80f && absorptionPercentage < 95f)
        {
            targetTexture = waterData.texture80;
        }
        
        if (targetTexture != null)
        {
            // Sử dụng Material Handler để thay đổi texture
            materialHandler.ChangeBaseMap(targetTexture);
            
            if (debugPhysics) 
                Debug.Log($"[FlowerController] Updated texture to {targetTexture.name} (Absorption: {absorptionPercentage:F1}%)", this);
        }
    }
    
    /// <summary>
    /// Lưu vị trí và rotation ban đầu
    /// </summary>
    private void SaveInitialPosition()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        hasInitialPositionSaved = true;
        
        if (debugPhysics)
            Debug.Log($"[FlowerController] Saved initial position: {initialPosition}, rotation: {initialRotation.eulerAngles}", this);
    }
    
    /// <summary>
    /// Khôi phục về vị trí và rotation ban đầu
    /// </summary>
    public void RestartPosition()
    {
        if (!hasInitialPositionSaved)
        {
            if (debugPhysics)
                Debug.LogWarning("[FlowerController] Cannot restart position - initial position not saved yet!", this);
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
        
        if (debugPhysics)
            Debug.Log($"[FlowerController] Position restarted to: {initialPosition}, rotation: {initialRotation.eulerAngles}", this);
    }
}