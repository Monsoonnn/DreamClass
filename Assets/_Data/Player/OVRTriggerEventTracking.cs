using UnityEngine;
using UnityEngine.Events;

public class OVRTriggerEventTracking : OVRButtonHandlerBase
{
    [Header("Custom Unity Events")]
    public UnityEvent OnTriggered;           // Không tham số
    public UnityEvent<string> OnTriggeredMsg; // Có tham số


    protected override void OnButtonPressed()
    {

        // Gọi event trong Inspector
        OnTriggered?.Invoke();

        // Event có tham số
        OnTriggeredMsg?.Invoke("Player respawned");
    }
}
