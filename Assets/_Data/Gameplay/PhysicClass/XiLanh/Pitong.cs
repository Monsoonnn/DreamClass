using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class Pitong : NewMonobehavior {
    [Header("Movement Range")]
    public float topY = 0.28f;       // Highest position (0%)
    public float bottomY = -0.555f;  // Lowest position (100%)

    [Header("Status")]
    public float percent;            // Compression percentage (0–100%)
    public bool isHandTouching;      // True when a hand is inside trigger

    private Rigidbody rb;
    private BoxCollider boxCollider;
    private Vector3 basePos;
    private Quaternion baseRot;

    protected override void Start() {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();

        // Setup collider as trigger for hand detection
        boxCollider.isTrigger = true;

        // Save initial transform
        basePos = transform.position;
        baseRot = transform.rotation;
    }

    private void FixedUpdate() {
        if (!isHandTouching) return; // Only process when hand is touching

        // Clamp movement to Y axis only
        Vector3 pos = transform.position;
        pos.x = basePos.x;
        pos.z = basePos.z;
        pos.y = Mathf.Clamp(pos.y, bottomY, topY);

        rb.MovePosition(pos);
        rb.MoveRotation(baseRot);

        // Calculate compression percent
        percent = Mathf.InverseLerp(topY, bottomY, pos.y) * 100f;
    }

    private void OnTriggerEnter( Collider other ) {
        // Check if the object is a VR hand
        if (other.CompareTag("Hand")) {
            isHandTouching = true;
        }
    }

    private void OnTriggerExit( Collider other ) {
        if (other.CompareTag("Hand")) {
            isHandTouching = false;
        }
    }
}
