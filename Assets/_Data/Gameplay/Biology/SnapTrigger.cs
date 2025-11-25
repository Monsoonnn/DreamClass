using UnityEngine;
using System.Collections;

/// <summary>
/// Component con của Flower - phát hiện và xử lý snap vào Glass
/// Collider của component này dùng để detect Glass
/// </summary>
[RequireComponent(typeof(Collider))]
public class SnapTrigger : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugSnap = false;

    [Header("Snap Settings")]
    [SerializeField] private float snapDetectionRange = 1f; // Khoảng cách nhìn thấy ModelSnap
    [SerializeField] private float snapActivationRange = 0.25f; // Khoảng cách để snap vào
    [SerializeField] private float snapSpeed = 5f; // Tốc độ move đến vị trí snap

    [SerializeField] private ForceHand forceHand;
    [SerializeField] private GameObject ISDK_RayGrabInteraction;
    [SerializeField] private GameObject ISDK_HandGrabInteraction;

    private GlassController nearbyGlass = null;
    private bool isSnapped = false;
    private FlowerController flowerController;
    private Collider snapCollider;
    
    private Coroutine moveCoroutine;
    
    void Start()
    {
        flowerController = GetComponentInParent<FlowerController>();
        snapCollider = GetComponent<Collider>();
        snapCollider.isTrigger = true;

        if (flowerController == null)
        {
            Debug.LogError("[SnapTrigger] FlowerController not found in parent!", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (isSnapped) return;

        DetectNearbyGlass();
        HandleSnapping();
    }

    /// <summary>
    /// Phát hiện Glass gần đó trong phạm vi
    /// </summary>
    private void DetectNearbyGlass()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, snapDetectionRange);
        GlassController foundGlass = null;

        foreach (Collider col in colliders)
        {
            GlassController glass = col.GetComponent<GlassController>();
            // Chỉ chấp nhận glass nếu chưa có flower nào kết nối
            if (glass != null && glass.connectedFlower == null)
            {
                foundGlass = glass;
                break;
            }
        }

        // Cập nhật trạng thái
        if (foundGlass != nearbyGlass)
        {
            if (nearbyGlass != null)
            {
                nearbyGlass.SetFlowerNearby(false);
                if (debugSnap) Debug.Log("[SnapTrigger] Flower left Glass detection range", this);
            }

            nearbyGlass = foundGlass;

            if (nearbyGlass != null)
            {
                nearbyGlass.SetFlowerNearby(true);
                if (debugSnap) Debug.Log("[SnapTrigger] Flower entered Glass detection range", this);
            }
        }
    }

    /// <summary>
    /// Xử lý logic snap vào Glass
    /// </summary>
    private void HandleSnapping()
    {
        if (nearbyGlass != null && !nearbyGlass.IsEmpty() && !isSnapped)
        {
            Vector3 snapPos = nearbyGlass.GetSnapVisualPosition();
            float distanceToSnap = Vector3.Distance(transform.position, snapPos);

            if (distanceToSnap <= snapActivationRange)
            {
                isSnapped = true;
                if (debugSnap) Debug.Log("[SnapTrigger] Start moving to snap position", this);
                SnapToGlass();
            }
        }
        return;
    }

    /// <summary>
    /// Di chuyển Flower đến vị trí snap - chỉ move một lần
    /// </summary>
    private void MoveTowardsSnap()
    {
        // Vector3 direction = (targetPos - transform.position).normalized;
        // flowerController.transform.Translate(direction * snapSpeed * Time.deltaTime, Space.World);
        // float distanceToTarget = Vector3.Distance(flowerController.transform.position, targetPos);
        flowerController.DisablePhysics();
        nearbyGlass.DisableInteractions();
        flowerController.transform.position = nearbyGlass.modelSnapVisual.transform.position;
        flowerController.transform.rotation = Quaternion.identity;
       
    }

    /// <summary>
    /// Snap Flower vào vị trí ModelSnap và tắt nó
    /// </summary>
    private void SnapToGlass()
    {
        if (debugSnap) Debug.Log("[SnapTrigger] Flower snapped to Glass!", this);

        isSnapped = true;

        // Đặt Flower vào vị trí snap visual
        Vector3 snapPos = nearbyGlass.GetSnapVisualPosition();
        flowerController.transform.position = snapPos;
        
        // Reset rotation về Quaternion.identity
        flowerController.transform.rotation = Quaternion.identity;

        // Tắt collider của SnapTrigger
        snapCollider.enabled = false;

        // Set connectedFlower cho Glass
        nearbyGlass.connectedFlower = flowerController;
        
        // Yêu cầu Glass tắt ModelSnap visual
        nearbyGlass.HideSnapVisual();

        // Thông báo cho FlowerController
        flowerController.OnSnapped(nearbyGlass);
        DisableHandInteractions();
        
        MoveTowardsSnap();
    }

    public void DisableHandInteractions()
    {
        if (forceHand != null)
        {
            forceHand.DetachFromHand();
        }

        if (ISDK_RayGrabInteraction != null)
        {
            ISDK_RayGrabInteraction.SetActive(false);
        }

        if (ISDK_HandGrabInteraction != null)
        {
            ISDK_HandGrabInteraction.SetActive(false);
        }
    }

    public void EnableHandInteractions()
    {

        if (ISDK_RayGrabInteraction != null)
        {
            ISDK_RayGrabInteraction.SetActive(true);
        }

        if (ISDK_HandGrabInteraction != null)
        {
            ISDK_HandGrabInteraction.SetActive(true);
        }
    }

    /// <summary>
    /// Unsnap Flower khỏi Glass
    /// </summary>
    public void Unsnap()
    {
        if (!isSnapped) return;

        if (debugSnap) Debug.Log("[SnapTrigger] Flower unsnapped from Glass", this);

        // Clear connectedFlower từ Glass
        if (nearbyGlass != null)
        {
            nearbyGlass.connectedFlower = null;
        }

        isSnapped = false;
        
        // Stop coroutine nếu còn đang chạy
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        // Bật lại physics
        //flowerController.EnablePhysics();

        // Bật lại collider
        snapCollider.enabled = true;

        nearbyGlass = null;
    }

    public bool IsSnapped()
    {
        return isSnapped;
    }

    public GlassController GetNearbyGlass()
    {
        return nearbyGlass;
    }

    // Visualize detection range
    private void OnDrawGizmos()
    {
        // Detection range
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, snapDetectionRange);

        // Snap activation range
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, snapActivationRange);
    }
}
