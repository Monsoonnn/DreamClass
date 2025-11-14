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
    [SerializeField] private bool isFlowing = true;
    [SerializeField] private float flowRate = 100f; // Số lượng nước chảy ra mỗi giây
    
    private Vector3 streamEndPoint;
    
    void Start()
    {
        SetupWaterStream();
        SetupParticles();
    }
    
    void Update()
    {
        if (isFlowing)
        {
            UpdateWaterStream();
        }
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
    
    private void UpdateWaterStream()
    {
        if (waterSource == null)
            waterSource = transform;
        
        Vector3 startPoint = waterSource.position;
        
        // Tính toán điểm kết thúc với hiệu ứng rơi tự do
        for (int i = 0; i < waterLine.positionCount; i++)
        {
            float t = i / (float)(waterLine.positionCount - 1);
            float distance = streamLength * t;
            
            // Công thức rơi tự do: y = y0 - 0.5 * g * t^2
            float time = distance / waterSpeed;
            float dropDistance = 0.5f * 9.81f * time * time;
            
            Vector3 point = startPoint + Vector3.down * distance;
            point.y -= dropDistance * 0.1f; // Giảm hiệu ứng rơi để trông tự nhiên hơn
            
            waterLine.SetPosition(i, point);
        }
        
        streamEndPoint = waterLine.GetPosition(waterLine.positionCount - 1);
    }
    
    public void StartFlow()
    {
        isFlowing = true;
        if (waterParticles != null)
            waterParticles.Play();
    }
    
    public void StopFlow()
    {
        isFlowing = false;
        if (waterParticles != null)
            waterParticles.Stop();
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
    
    public bool IsFlowing()
    {
        return isFlowing;
    }
    
    // Vẽ Gizmo để debug
    private void OnDrawGizmos()
    {
        if (waterSource != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(waterSource.position, 0.1f);
            Gizmos.DrawLine(waterSource.position, waterSource.position + Vector3.down * streamLength);
        }
    }
}