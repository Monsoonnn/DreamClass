using UnityEngine;

public abstract class OVRButtonHandlerBase : MonoBehaviour
{
    [Header("Controller Settings")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Button to Track")]
    public OVRInput.Button buttonToTrack = OVRInput.Button.PrimaryIndexTrigger;

    private bool wasPressed = false;

    protected virtual void Update()
    {
        bool isPressed = OVRInput.Get(buttonToTrack, controller);

        // Detect button down event (pressed this frame)
        if (isPressed && !wasPressed)
        {
            OnButtonPressed();
        }

        // Detect button up event (released this frame)
        if (!isPressed && wasPressed)
        {
            OnButtonReleased();
        }

        wasPressed = isPressed;
    }

    // Event methods for subclasses to override
    protected abstract void OnButtonPressed();
    protected virtual void OnButtonReleased() { }
}

