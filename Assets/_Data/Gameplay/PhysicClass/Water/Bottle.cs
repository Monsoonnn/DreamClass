using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class Bottle : NewMonobehavior {
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private GameObject liquidObject;

    [Header("Liquid Settings")]
    [SerializeField] private float maxLiquid = 75f;
    [SerializeField] private float currentLiquid = 100f;

    public float MaxLiquid => maxLiquid;
    public float CurrentLiquid => currentLiquid;


    [Header("Liquid Transform")]
    [SerializeField] private Vector3 baseScale;
    [SerializeField] private Vector3 basePosition;

   
    private const float positionFactor = 1.2714f;

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadBoxCollider();
        this.LoadLiquidObject();
    }

    protected virtual void LoadBoxCollider() {
        if (boxCollider != null) return;
        boxCollider = transform.GetComponent<BoxCollider>();
    }

    protected virtual void LoadRigidbody() { 
        if(rb != null) return;
        rb = transform.GetComponent<Rigidbody>();
    }

    protected virtual void LoadLiquidObject() {
        if (liquidObject != null) return;
        Transform liquid = transform.Find("Liquid");
        if (liquid != null) liquidObject = liquid.gameObject;
        baseScale = new Vector3(0.8f, 1f, 1f);
        basePosition = Vector3.zero;
    }

    [ProButton]
    public void UpdateLiquidLevel( float newLiquid ) {
        if (liquidObject == null) return;

        if(newLiquid > 0) liquidObject.SetActive(true);
        else liquidObject.SetActive(false);

        currentLiquid = Mathf.Clamp(newLiquid, 0f, maxLiquid);

       
        float ratio = currentLiquid / maxLiquid;
        
        float scaleY = ratio;

        float offsetY = (1 - scaleY) * positionFactor;

        liquidObject.transform.localScale = new Vector3(baseScale.x, baseScale.y * scaleY, baseScale.z);
        liquidObject.transform.localPosition = new Vector3(basePosition.x, basePosition.y + offsetY, basePosition.z);
    }
}
