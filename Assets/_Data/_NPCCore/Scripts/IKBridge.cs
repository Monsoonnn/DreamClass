using UnityEngine;

/// <summary>
/// Script này phải đặt trên GameObject có Animator component.
/// Nó sẽ forward OnAnimatorIK callback lên HeadLookPlayer ở parent.
/// </summary>
public class IKBridge : MonoBehaviour
{
    private HeadLookPlayer headLookPlayer;
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        
        if (animator == null)
        {
            Debug.LogError("IKBridge: Không tìm thấy Animator component!");
            enabled = false;
            return;
        }

        // Tìm HeadLookPlayer ở parent
        headLookPlayer = transform.parent.GetComponentInChildren<HeadLookPlayer>();
        
        if (headLookPlayer == null)
        {
            Debug.LogError("IKBridge: Không tìm thấy HeadLookPlayer ở parent!");
            enabled = false;
            return;
        }

        // Set animator reference cho HeadLookPlayer
        headLookPlayer.SetAnimator(animator);
        
        Debug.Log($"IKBridge: Connected to {headLookPlayer.gameObject.name}");
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (headLookPlayer != null)
        {
            headLookPlayer.ProcessIK(layerIndex);
        }
    }
}