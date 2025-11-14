using UnityEngine;

public class HeadLookPlayer : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Tag của Player (mặc định: 'Player')")]
    public string playerTag = "Player";

    [Tooltip("Tự động tìm Player trong trigger collider")]
    public bool autoDetectPlayer = true;

    [Tooltip("Có thể gán thủ công nếu không dùng trigger")]
    public Transform manualTarget;

    [Tooltip("Transform để tính hướng nhìn (thường là root hoặc body). Nếu null sẽ dùng transform này")]
    public Transform forwardReference;

    [Header("Head Bone Settings")]
    [Tooltip("Bone của đầu (Head bone)")]
    public Transform headBone;

    [Tooltip("Bone của cổ (Neck bone) - Optional")]
    public Transform neckBone;

    [Header("Look At Settings")]
    [Range(0f, 1f)]
    [Tooltip("Trọng số của hiệu ứng Look At (0 = không nhìn, 1 = nhìn hoàn toàn)")]
    public float lookAtWeight = 1f;

    [Range(0f, 180f)]
    [Tooltip("Góc tối đa có thể xoay đầu")]
    public float maxAngle = 80f;

    [Tooltip("Chỉ xoay theo trục ngang (trái/phải), không xoay dọc (trên/dưới)")]
    public bool horizontalOnly = true;

    [Range(0f, 1f)]
    [Tooltip("Body weight - Ảnh hưởng đến toàn bộ cơ thể (0 = chỉ đầu, 1 = cả thân)")]
    public float bodyWeight = 0f;

    [Range(0f, 1f)]
    [Tooltip("Head weight - Ảnh hưởng đến đầu (1 = xoay tối đa)")]
    public float headWeight = 1f;

    [Range(0f, 1f)]
    [Tooltip("Eyes weight - Ảnh hưởng đến mắt")]
    public float eyesWeight = 1f;

    [Range(0f, 1f)]
    [Tooltip("Clamp weight - Giới hạn góc xoay (0.5 = 50% giới hạn)")]
    public float clampWeight = 0.5f;

    [Header("Lower Body Settings")]
    [Tooltip("Xoay hông (Hips) theo target")]
    public bool rotateHips = false;

    [Range(0f, 1f)]
    [Tooltip("Mức độ xoay hông (0 = không xoay, 1 = xoay hoàn toàn)")]
    public float hipsRotationWeight = 0.5f;

    [Tooltip("Chỉ xoay hông theo trục Y (ngang)")]
    public bool hipsYAxisOnly = true;

    [Header("Smooth Settings")]
    [Tooltip("Tốc độ chuyển động mượt mà")]
    public float smoothSpeed = 5f;

    [Tooltip("Update mỗi N frames (1 = mỗi frame, 2 = mỗi 2 frames, etc.)")]
    [Range(1, 10)]
    public int updateFrequency = 1;

    [Header("Debug")]
    [Tooltip("Hiển thị debug info")]
    public bool showDebug = true;

    private Animator animator;
    private Transform currentTarget;
    private float currentWeight = 0f;
    private bool playerInRange = false;
    private Vector3 cachedLookPosition;
    private int frameCounter = 0;
    private Transform hipsBone;

    void Start()
    {
        // Tìm Animator trên GameObject này hoặc trong children
        animator = transform.parent.GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("HeadLookPlayer: Không tìm thấy Animator. Sẽ chờ IKBridge set animator.");
            // Không disable, vì có thể IKBridge sẽ set animator sau
        }
        else
        {
            InitializeBones();
        }
    }

    void InitializeBones()
    {
        if (animator == null) return;

        if (showDebug)
        {
            Debug.Log($"HeadLookPlayer: Found Animator on {animator.gameObject.name}");
        }

        // Tự động set forward reference nếu chưa có
        if (forwardReference == null)
        {
            forwardReference = transform; // Dùng root transform
            if (showDebug)
            {
                Debug.Log($"HeadLookPlayer: Using {forwardReference.name} as forward reference");
            }
        }

        // Tự động tìm head bone nếu không được gán
        if (headBone == null && animator != null)
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
            {
                Debug.LogWarning("HeadLookPlayer: Không tìm thấy Head bone!");
            }
        }

        // Tự động tìm neck bone
        if (neckBone == null && animator != null)
        {
            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
        }

        // Tự động tìm hips bone
        if (hipsBone == null && animator != null)
        {
            hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsBone != null && showDebug)
            {
                Debug.Log($"HeadLookPlayer: Found Hips bone at {hipsBone.name}");
            }
        }

        // Kiểm tra có Collider với isTrigger không
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
        }

        if (autoDetectPlayer && col != null && !col.isTrigger)
        {
            Debug.LogWarning("HeadLookPlayer: Collider cần set isTrigger = true để phát hiện Player!");
        }

        // Nếu có manual target thì dùng luôn
        if (manualTarget != null)
        {
            currentTarget = manualTarget;
            playerInRange = true;
        }
    }

    // === PUBLIC METHODS FOR IK BRIDGE ===

    /// <summary>
    /// Được gọi từ IKBridge để set animator reference
    /// </summary>
    public void SetAnimator(Animator anim)
    {
        animator = anim;
        InitializeBones();
    }

    /// <summary>
    /// Được gọi từ IKBridge trong OnAnimatorIK
    /// </summary>
    public void ProcessIK(int layerIndex)
    {
        OnAnimatorIK(layerIndex);
    }

    void Update()
    {
        // Xác định target cuối cùng
        Transform finalTarget = autoDetectPlayer ? currentTarget : manualTarget;

        // Smooth transition của weight
        if (finalTarget != null && playerInRange && IsTargetInRange(finalTarget))
        {
            currentWeight = Mathf.Lerp(currentWeight, lookAtWeight, Time.deltaTime * smoothSpeed);
        }
        else
        {
            currentWeight = Mathf.Lerp(currentWeight, 0f, Time.deltaTime * smoothSpeed);
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null)
        {
            if (showDebug)
                Debug.LogError("HeadLookPlayer: Animator is null!");
            return;
        }

        Transform finalTarget = autoDetectPlayer ? currentTarget : manualTarget;

        if (finalTarget != null && playerInRange)
        {
            // Chỉ update vị trí mỗi N frames
            frameCounter++;
            if (frameCounter >= updateFrequency)
            {
                frameCounter = 0;

                // Tính toán vị trí target
                cachedLookPosition = finalTarget.position;

                // Nếu chỉ nhìn ngang, giữ nguyên độ cao của đầu
                if (horizontalOnly && headBone != null)
                {
                    cachedLookPosition.y = headBone.position.y;
                }
            }

            // Debug info
            if (showDebug && Time.frameCount % 60 == 0) // Log mỗi 60 frames
            {
                Debug.Log($"HeadLookPlayer IK: Target={finalTarget.name}, Weight={currentWeight:F2}, InRange={IsTargetInRange(finalTarget)}, UpdateFreq={updateFrequency}, RotateHips={rotateHips}");
            }

            // Sử dụng IK với vị trí đã cache
            animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
            animator.SetLookAtPosition(cachedLookPosition);

            // Xoay hông nếu được bật
            if (rotateHips && hipsBone != null && currentWeight > 0.01f)
            {
                RotateHipsTowardsTarget(finalTarget);
            }
        }
        else
        {
            // Reset về 0 khi không có target
            animator.SetLookAtWeight(0);
            frameCounter = 0;

            if (showDebug && Time.frameCount % 120 == 0)
            {
                Debug.Log($"HeadLookPlayer: No target (finalTarget={finalTarget != null}, playerInRange={playerInRange})");
            }
        }
    }

    void RotateHipsTowardsTarget(Transform target)
    {
        if (hipsBone == null || target == null)
            return;

        // Lưu rotation gốc
        Quaternion originalRotation = hipsBone.rotation;

        // Tính hướng từ hông đến target
        Vector3 directionToTarget = target.position - hipsBone.position;

        // Nếu chỉ xoay theo trục Y
        if (hipsYAxisOnly)
        {
            directionToTarget.y = 0;
        }

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            // Tính rotation mục tiêu
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

            // Nếu chỉ xoay Y axis, giữ nguyên X và Z
            if (hipsYAxisOnly)
            {
                Vector3 currentEuler = originalRotation.eulerAngles;
                Vector3 targetEuler = targetRotation.eulerAngles;
                targetRotation = Quaternion.Euler(currentEuler.x, targetEuler.y, currentEuler.z);
            }

            // Lerp với weight
            hipsBone.rotation = Quaternion.Slerp(
                originalRotation,
                targetRotation,
                hipsRotationWeight * currentWeight
            );
        }
    }

    // Phát hiện Player vào trigger
    void OnTriggerEnter(Collider other)
    {
        if (!autoDetectPlayer)
            return;

        if (other.CompareTag(playerTag))
        {
            currentTarget = other.transform;
            playerInRange = true;

            if (showDebug)
            {
                Debug.Log($"HeadLookPlayer: Player đã vào vùng phát hiện - {other.name}");
            }
        }
    }

    // Player rời khỏi trigger
    void OnTriggerExit(Collider other)
    {
        if (!autoDetectPlayer)
            return;

        if (other.CompareTag(playerTag))
        {
            playerInRange = false;

            if (showDebug)
            {
                Debug.Log($"HeadLookPlayer: Player đã rời khỏi vùng phát hiện - {other.name}");
            }

            // Delay một chút trước khi clear target để có hiệu ứng mượt
            Invoke(nameof(ClearTarget), 0.5f);
        }
    }

    void ClearTarget()
    {
        if (!playerInRange)
        {
            currentTarget = null;
        }
    }

    // Kiểm tra xem target có nằm trong phạm vi góc nhìn không
    bool IsTargetInRange(Transform target)
    {
        if (target == null || forwardReference == null)
            return false;

        // Dùng forward của body/root, KHÔNG dùng head forward
        Vector3 bodyForward = forwardReference.forward;

        // Tính hướng từ head đến target (cho khoảng cách)
        Vector3 directionToTarget = target.position - (headBone != null ? headBone.position : forwardReference.position);

        // Nếu chỉ xoay ngang, bỏ qua trục Y
        if (horizontalOnly)
        {
            bodyForward.y = 0;
            directionToTarget.y = 0;
            bodyForward.Normalize();
            directionToTarget.Normalize();
        }

        float angle = Vector3.Angle(bodyForward, directionToTarget);

        return angle <= maxAngle;
    }

    // === PUBLIC METHODS ===

    /// <summary>
    /// Bật/tắt Look At
    /// </summary>
    public void SetLookAtEnabled(bool enabled)
    {
        lookAtWeight = enabled ? 1f : 0f;
    }

    /// <summary>
    /// Set target thủ công (tắt auto detect)
    /// </summary>
    public void SetManualTarget(Transform target)
    {
        manualTarget = target;
        autoDetectPlayer = false;
        playerInRange = target != null;
    }

    /// <summary>
    /// Bật lại auto detect
    /// </summary>
    public void EnableAutoDetect()
    {
        autoDetectPlayer = true;
        manualTarget = null;
    }

    /// <summary>
    /// Kiểm tra có đang nhìn Player không
    /// </summary>
    public bool IsLookingAtPlayer()
    {
        return playerInRange && currentTarget != null && currentWeight > 0.1f;
    }

    // Vẽ debug để xem hướng nhìn và vùng trigger
    void OnDrawGizmos()
    {
        if (!showDebug)
            return;

        Transform refTransform = forwardReference != null ? forwardReference : transform;

        // Vẽ line đến target
        if (headBone != null && currentTarget != null && playerInRange)
        {
            Vector3 lookPos = currentTarget.position;

            // Nếu horizontal only, vẽ line tới vị trí đã điều chỉnh
            if (horizontalOnly)
            {
                lookPos.y = headBone.position.y;

                // Vẽ line gốc (mờ)
                Gizmos.color = new Color(1, 1, 0, 0.3f);
                Gizmos.DrawLine(headBone.position, currentTarget.position);
            }

            // Vẽ line chính
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(headBone.position, lookPos);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lookPos, 0.2f);
        }

        // Vẽ cone góc nhìn từ BODY FORWARD (không phải head forward)
        if (refTransform != null)
        {
            Vector3 startPos = headBone != null ? headBone.position : refTransform.position;

            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Vector3 forward = refTransform.forward;

            // Chỉ vẽ góc ngang nếu horizontal only
            if (horizontalOnly)
            {
                forward.y = 0;
                forward.Normalize();
            }

            Vector3 right = Quaternion.Euler(0, maxAngle, 0) * forward;
            Vector3 left = Quaternion.Euler(0, -maxAngle, 0) * forward;

            Gizmos.DrawRay(startPos, forward * 3f);
            Gizmos.DrawRay(startPos, right * 3f);
            Gizmos.DrawRay(startPos, left * 3f);

            // Vẽ marker cho forward reference
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(refTransform.position, 0.1f);
            Gizmos.DrawRay(refTransform.position, refTransform.forward * 1f);

            // Vẽ hips direction nếu rotate hips được bật
            if (rotateHips && hipsBone != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(hipsBone.position, 0.15f);
                Gizmos.DrawRay(hipsBone.position, hipsBone.forward * 1.5f);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Vẽ trigger collider khi được chọn
        Collider col = GetComponent<Collider>();
        if (col == null)
            col = GetComponentInChildren<Collider>();

        if (col != null && col.isTrigger)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);

            if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius * transform.localScale.x);
            }
            else if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}