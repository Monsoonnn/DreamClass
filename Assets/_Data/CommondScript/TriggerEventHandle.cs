using UnityEngine;
using UnityEngine.Events;

public class TriggerEventHandler : MonoBehaviour
{
    [Header("Events on Trigger Enter (Player only)")]
    public UnityEvent onTriggerEnterEvents;

    [Header("Events on Trigger Exit (Player only)")]
    public UnityEvent onTriggerExitEvents;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        onTriggerEnterEvents?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        onTriggerExitEvents?.Invoke();
    }
}
