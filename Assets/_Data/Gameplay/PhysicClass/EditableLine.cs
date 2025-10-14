using UnityEngine;

[ExecuteAlways]
public class EditableLineRenderer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Vector3[] points;

    private void OnEnable() {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null) return;

        points = new Vector3[lineRenderer.positionCount];
        lineRenderer.GetPositions(points);
    }


    void Update()
    {
        if (lineRenderer == null || points == null) return;
        if (lineRenderer.positionCount != points.Length)
            lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }
}
