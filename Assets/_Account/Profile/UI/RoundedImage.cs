using UnityEngine;
using UnityEngine.UI;

namespace DreamClass.Account.UI
{
    /// <summary>
    /// Component để tạo border radius cho UI Image
    /// Sử dụng shader UI/RoundedImage
    /// </summary>
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class RoundedImage : MonoBehaviour
    {
        [Header("Corner Radius")]
        [Range(0f, 0.5f)]
        [SerializeField] private float radius = 0.1f;

        [Header("Settings")]
        [SerializeField] private bool useCircle = false;

        private Image image;
        private Material materialInstance;

        private static readonly int RadiusProperty = Shader.PropertyToID("_Radius");

        public float Radius
        {
            get => radius;
            set
            {
                radius = Mathf.Clamp(value, 0f, 0.5f);
                UpdateRadius();
            }
        }

        public bool UseCircle
        {
            get => useCircle;
            set
            {
                useCircle = value;
                if (useCircle) radius = 0.5f;
                UpdateRadius();
            }
        }

        private void Awake()
        {
            SetupMaterial();
        }

        private void OnEnable()
        {
            SetupMaterial();
        }

        private void OnValidate()
        {
            if (useCircle) radius = 0.5f;
            SetupMaterial();
            UpdateRadius();
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(materialInstance);
                else
                    DestroyImmediate(materialInstance);
            }
        }

        private void SetupMaterial()
        {
            if (image == null)
                image = GetComponent<Image>();

            if (image == null) return;

            // Find or create material
            Shader shader = Shader.Find("UI/RoundedImage");
            if (shader == null)
            {
                Debug.LogWarning("[RoundedImage] Shader 'UI/RoundedImage' not found!");
                return;
            }

            if (materialInstance == null)
            {
                materialInstance = new Material(shader);
                materialInstance.name = "RoundedImage_Instance";
            }

            image.material = materialInstance;
            UpdateRadius();
        }

        private void UpdateRadius()
        {
            if (materialInstance != null)
            {
                materialInstance.SetFloat(RadiusProperty, radius);
            }
        }

        /// <summary>
        /// Set radius as percentage (0-100)
        /// </summary>
        public void SetRadiusPercent(float percent)
        {
            Radius = percent / 100f * 0.5f;
        }

        /// <summary>
        /// Make the image circular
        /// </summary>
        public void MakeCircular()
        {
            UseCircle = true;
        }

        /// <summary>
        /// Reset to square corners
        /// </summary>
        public void MakeSquare()
        {
            Radius = 0f;
            useCircle = false;
        }
    }
}
