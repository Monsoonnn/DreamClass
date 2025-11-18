using UnityEngine;
using System.Collections;

public class WaterStream : MonoBehaviour
{
    [Header("Water Stream Settings")]
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private LineRenderer waterLine;
    [SerializeField] private Transform waterSource; // Điểm xuất phát của nước
    [SerializeField] private float streamLength = 5f;
    [SerializeField] private float waterSpeed = 2f;
    
    [Header("Visual Settings")]
    [SerializeField] private Color waterColor = new Color(0.3f, 0.6f, 1f, 0.7f);
    [SerializeField] private float streamWidth = 0.1f;
    [SerializeField] private Material waterMaterial;
    
    [Header("Flow Control")]
    [SerializeField] private bool isFlowing = false; // Changed to false by default
    [SerializeField] private float flowRate = 100f; // Số lượng nước chảy ra mỗi giây
    [SerializeField] private float transitionSpeed = 2f; // Speed of flow animation
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip flowingSound;
    [SerializeField] private bool loopAudio = true;
    
    [Header("Dynamic Stream")]
    private Vector3 streamEndPoint;
    private Vector3 dynamicEndPoint; // Override endpoint when catching in cup
    private bool hasCustomEndPoint = false;
    private WaterCup catchingCup = null;
    private float defaultStreamLength;
    private float currentVisualLength = 0f; // Current animated length
    private float targetLength = 0f; // Target length to animate to
    
    void Start()
    {
        defaultStreamLength = streamLength;
        SetupWaterStream();
        SetupParticles();
        SetupAudio();
        
        // Don't initialize flow - wait for StartFlow() call
        currentVisualLength = 0f;
        targetLength = 0f;
        
        // Hide stream initially
        if (waterLine != null)
            waterLine.enabled = false;
        if (waterParticles != null)
            waterParticles.Stop();
    }
    
    void Update()
    {
        // Animate length transition
        AnimateStreamLength();
        
        // Update stream visual if there's any length
        if (currentVisualLength > 0.01f)
        {
            UpdateWaterStream();
        }
        
        // Update audio volume based on flow
        UpdateAudio();
    }
    
    private void SetupWaterStream()
    {
        // Tạo LineRenderer nếu chưa có
        if (waterLine == null)
        {
            GameObject lineObj = new GameObject("WaterLine");
            lineObj.transform.SetParent(transform);
            waterLine = lineObj.AddComponent<LineRenderer>();
        }
        
        // Cấu hình LineRenderer
        waterLine.startWidth = streamWidth;
        waterLine.endWidth = streamWidth * 0.6f;
        waterLine.positionCount = 20;
        waterLine.material = waterMaterial != null ? waterMaterial : new Material(Shader.Find("Sprites/Default"));
        waterLine.startColor = waterColor;
        waterLine.endColor = waterColor;
        waterLine.useWorldSpace = true;
        waterLine.enabled = false; // Start disabled
        
        // Thêm hiệu ứng uốn cong cho dòng nước
        waterLine.textureMode = LineTextureMode.Tile; 
    }
    
    private void SetupParticles()
    {
        // Tạo Particle System nếu chưa có
        if (waterParticles == null)
        {
            GameObject particleObj = new GameObject("WaterParticles");
            particleObj.transform.SetParent(transform);
            waterParticles = particleObj.AddComponent<ParticleSystem>();
        }
        
        // Cấu hình Particle System
        var main = waterParticles.main;
        main.startColor = waterColor;
        main.startSize = 0.05f;
        main.startSpeed = waterSpeed;
        main.startLifetime = streamLength / waterSpeed;
        main.maxParticles = 1000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = waterParticles.emission;
        emission.rateOverTime = flowRate;
        
        var shape = waterParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f;
        shape.radius = 0.05f;
        
        // Thêm trọng lực
        var forceOverLifetime = waterParticles.forceOverLifetime;
        forceOverLifetime.enabled = true;
        forceOverLifetime.y = -9.81f;
    }
    
    private void SetupAudio()
    {
        // Tạo AudioSource nếu chưa có
        if (audioSource == null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Cấu hình AudioSource
        if (flowingSound != null)
        {
            audioSource.clip = flowingSound;
        }
        audioSource.loop = loopAudio;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f; // Start silent
        audioSource.spatialBlend = 0.5f; // 3D sound
    }
    
    private void AnimateStreamLength()
    {
        // Determine target length based on flow state
        if (isFlowing)
        {
            if (hasCustomEndPoint)
            {
                // Calculate distance to custom endpoint (water surface)
                float distanceToSurface = Mathf.Abs(GetStreamStartPoint().y - dynamicEndPoint.y);
                targetLength = distanceToSurface;
            }
            else
            {
                targetLength = streamLength;
            }
        }
        else
        {
            targetLength = 0f;
        }
        
        // Smoothly animate current length to target
        float previousLength = currentVisualLength;
        currentVisualLength = Mathf.MoveTowards(currentVisualLength, targetLength, transitionSpeed * Time.deltaTime);
        
        // Enable/disable visual components based on length
        if (waterLine != null)
        {
            waterLine.enabled = currentVisualLength > 0.01f;
        }
        
        // Handle particle system
        if (waterParticles != null)
        {
            if (isFlowing && currentVisualLength > 0.1f)
            {
                if (!waterParticles.isPlaying)
                    waterParticles.Play();
            }
            else
            {
                if (waterParticles.isPlaying)
                    waterParticles.Stop();
            }
        }
    }
    
    private void UpdateWaterStream()
    {
        if (waterSource == null)
            waterSource = transform;
        
        Vector3 startPoint = waterSource.position;
        
        // Use current visual length for animation
        float effectiveLength = currentVisualLength;
        
        // Always stream downward, only length changes
        // Calculate stream with gravity effect
        for (int i = 0; i < waterLine.positionCount; i++)
        {
            float t = i / (float)(waterLine.positionCount - 1);
            float distance = effectiveLength * t;
            float time = distance / waterSpeed;
            float dropDistance = 0.5f * 9.81f * time * time;
            
            Vector3 point = startPoint + Vector3.down * distance;
            point.y -= dropDistance * 0.1f;
            
            waterLine.SetPosition(i, point);
        }
        
        // Update stored end point
        streamEndPoint = waterLine.GetPosition(waterLine.positionCount - 1);
        
        // Update particle system to match stream length
        UpdateParticleSystemForLength(effectiveLength);
    }
    
    private void UpdateParticleSystemForLength(float length)
    {
        if (waterParticles == null) return;
        
        var main = waterParticles.main;
        main.startLifetime = length / waterSpeed;
    }
    
    private void UpdateAudio()
    {
        if (audioSource == null) return;
        
        // Calculate target volume based on flow
        float targetVolume = 0f;
        if (isFlowing && currentVisualLength > 0.1f)
        {
            // Volume increases with stream length
            float lengthRatio = Mathf.Clamp01(currentVisualLength / streamLength);
            targetVolume = Mathf.Lerp(0.1f, 0.15f, lengthRatio);
        }
        
        // Smooth volume transition
        audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, Time.deltaTime * 2f);
        
        // Play/stop audio
        if (targetVolume > 0.01f && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if (targetVolume <= 0.01f && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
    
    public void SetCatchingCup(WaterCup cup)
    {
        catchingCup = cup;
        
        if (cup != null)
        {
            hasCustomEndPoint = true;
            
            // Check if cup is full - if so, stop the stream immediately
            if (cup.IsFull())
            {
                StopFlow();
                Debug.Log("[WaterStream] Cup is full, stopping stream");
            }
            else
            {
                Debug.Log("[WaterStream] Now pouring into cup");
            }
        }
        else
        {
            hasCustomEndPoint = false;
            Debug.Log("[WaterStream] Released from cup, returning to default length");
        }
    }
    
    public void SetStreamEndPoint(Vector3 endPoint)
    {
        dynamicEndPoint = endPoint;
        hasCustomEndPoint = true;
    }
    
    public void ResetStreamLength()
    {
        hasCustomEndPoint = false;
    }
    
    public void StartFlow()
    {
        isFlowing = true;
        Debug.Log("[WaterStream] Starting flow");
    }
    
    public void StopFlow()
    {
        isFlowing = false;
        Debug.Log("[WaterStream] Stopping flow");
        
        // Don't immediately reset - let animation handle it
        // Stream will gradually shrink to 0
    }
    
    public void SetFlowRate(float rate)
    {
        flowRate = Mathf.Max(0, rate);
        if (waterParticles != null)
        {
            var emission = waterParticles.emission;
            emission.rateOverTime = flowRate;
        }
    }
    
    public Vector3 GetStreamEndPoint()
    {
        return streamEndPoint;
    }
    
    public Vector3 GetStreamStartPoint()
    {
        if (waterSource == null)
            waterSource = transform;
        return waterSource.position;
    }
    
    public bool IsFlowing()
    {
        return isFlowing;
    }

    public void SetFlowing(bool state)
    {
        isFlowing = state;
    }
    
    
    public WaterCup GetCatchingCup()
    {
        return catchingCup;
    }
    
    public float GetCurrentStreamLength()
    {
        return currentVisualLength;
    }
    
    public float GetTargetStreamLength()
    {
        return targetLength;
    }
    
    // Vẽ Gizmo để debug
    private void OnDrawGizmos()
    {
        if (waterSource != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(waterSource.position, 0.1f);
            
            if (hasCustomEndPoint)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(waterSource.position, dynamicEndPoint);
                Gizmos.DrawWireSphere(dynamicEndPoint, 0.08f);
            }
            else
            {
                Gizmos.color = isFlowing ? Color.cyan : Color.gray;
                float debugLength = Application.isPlaying ? currentVisualLength : streamLength;
                Gizmos.DrawLine(waterSource.position, waterSource.position + Vector3.down * debugLength);
            }
        }
    }
}