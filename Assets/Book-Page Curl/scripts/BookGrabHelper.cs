/*using UnityEngine;

/// <summary>
/// Helper script để tự động tạo Grab Zones cho Book
/// Attach vào Book GameObject và nhấn "Create Grab Zones" trong Inspector
/// </summary>
[ExecuteInEditMode]
public class BookGrabZoneHelper : MonoBehaviour {
    public BookVR_Hybrid bookVR;
    public RectTransform bookPanel;

    [Header("Grab Zone Settings")]
    public float zoneOffsetFromEdge = 0.05f;
    public float zoneRadius = 0.1f;
    public float zoneHeight = 0.5f; // Tỷ lệ chiều cao của trang

    [Header("Visual Helpers")]
    public bool showGizmos = true;
    public Color rightZoneColor = new Color(1, 0, 0, 0.3f);
    public Color leftZoneColor = new Color(0, 0, 1, 0.3f);

    private Transform rightGrabZone;
    private Transform leftGrabZone;

    void OnValidate() {
        if (!bookVR) bookVR = GetComponent<BookVR_Hybrid>();
        if (!bookPanel && bookVR) bookPanel = bookVR.BookPanel;
    }

    [ContextMenu("Create Grab Zones")]
    public void CreateGrabZones() {
        if (!bookPanel) {
            Debug.LogError("BookPanel chưa được gán!");
            return;
        }

        // Tạo Right Page Grab Zone
        if (bookVR.rightPageGrabZone == null) {
            GameObject rightZone = new GameObject("RightPageGrabZone");
            rightZone.transform.SetParent(transform);
            rightGrabZone = rightZone.transform;
            bookVR.rightPageGrabZone = rightGrabZone;
        } else {
            rightGrabZone = bookVR.rightPageGrabZone;
        }

        // Tạo Left Page Grab Zone
        if (bookVR.leftPageGrabZone == null) {
            GameObject leftZone = new GameObject("LeftPageGrabZone");
            leftZone.transform.SetParent(transform);
            leftGrabZone = leftZone.transform;
            bookVR.leftPageGrabZone = leftGrabZone;
        } else {
            leftGrabZone = bookVR.leftPageGrabZone;
        }

        PositionGrabZones();
        bookVR.grabZoneRadius = zoneRadius;

        Debug.Log("✓ Grab Zones đã được tạo!");
    }

    [ContextMenu("Update Grab Zone Positions")]
    public void PositionGrabZones() {
        if (bookPanel == null || rightGrabZone == null || leftGrabZone == null) {
            Debug.LogWarning("Hãy tạo Grab Zones trước!");
            return;
        }

        float pageWidth = bookPanel.rect.width / 2f;
        float pageHeight = bookPanel.rect.height;

        // Right page grab zone (góc phải)
        Vector3 rightPos = bookPanel.TransformPoint(new Vector3(
            pageWidth - zoneOffsetFromEdge,
            -pageHeight * (0.5f - zoneHeight / 2f),
            0
        ));
        rightGrabZone.position = rightPos;

        // Left page grab zone (góc trái)
        Vector3 leftPos = bookPanel.TransformPoint(new Vector3(
            -pageWidth + zoneOffsetFromEdge,
            -pageHeight * (0.5f - zoneHeight / 2f),
            0
        ));
        leftGrabZone.position = leftPos;

        Debug.Log("✓ Grab Zones đã được cập nhật vị trí!");
    }

    void OnDrawGizmos() {
        if (!showGizmos) return;

        if (rightGrabZone != null) {
            Gizmos.color = rightZoneColor;
            Gizmos.DrawWireSphere(rightGrabZone.position, zoneRadius);
            Gizmos.DrawSphere(rightGrabZone.position, zoneRadius * 0.2f);

            // Draw label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(rightGrabZone.position + Vector3.up * 0.15f, "RIGHT GRAB");
#endif
        }

        if (leftGrabZone != null) {
            Gizmos.color = leftZoneColor;
            Gizmos.DrawWireSphere(leftGrabZone.position, zoneRadius);
            Gizmos.DrawSphere(leftGrabZone.position, zoneRadius * 0.2f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(leftGrabZone.position + Vector3.up * 0.15f, "LEFT GRAB");
#endif
        }

        // Draw page outline
        if (bookPanel != null) {
            Gizmos.color = Color.white;
            float w = bookPanel.rect.width / 2f;
            float h = bookPanel.rect.height / 2f;

            Vector3 tl = bookPanel.TransformPoint(new Vector3(-w, h, 0));
            Vector3 tr = bookPanel.TransformPoint(new Vector3(w, h, 0));
            Vector3 br = bookPanel.TransformPoint(new Vector3(w, -h, 0));
            Vector3 bl = bookPanel.TransformPoint(new Vector3(-w, -h, 0));

            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            Gizmos.DrawLine(bl, tl);

            // Center line
            Gizmos.color = Color.yellow;
            Vector3 top = bookPanel.TransformPoint(new Vector3(0, h, 0));
            Vector3 bottom = bookPanel.TransformPoint(new Vector3(0, -h, 0));
            Gizmos.DrawLine(top, bottom);
        }
    }
}*/