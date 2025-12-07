using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using DreamClass.Subjects;

namespace DreamClass.ResourceGame
{
    /// <summary>
    /// Resource Manager - Kiểm soát và tải tài nguyên trước khi vào game
    /// Flow: ResourceManager canvas → tải xong → SceneLoader canvas
    /// </summary>
    public class ResourceManager : SingletonCtrl<ResourceManager>
    {
        public static event Action OnResourcesReady;
        public static event Action<float> OnDownloadProgress;
        
        private static bool isResourcesReady = false;
        public static bool IsResourcesReady => isResourcesReady;

        [Header("UI - Resource Loading Canvas")]
        [SerializeField] private Image loadingBar;
        [SerializeField] private float fillSpeed = 0.5f;
        [SerializeField] private Canvas loadingCanvas;
        [SerializeField] private Camera loadingCamera;

        [Header("Settings")]
        [SerializeField] private bool autoCheckOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = true;

        // State
        private float targetProgress = 0f;
        private bool isLoading = false;
        private bool hasStartedCheck = false;

        void Update()
        {
            if (!isLoading) return;

            if (loadingBar != null)
            {
                float currentFillAmount = loadingBar.fillAmount;
                // IMPORTANT: Progress bar chỉ được tăng, KHÔNG bao giờ giảm
                float targetValue = Mathf.Max(currentFillAmount, targetProgress);
                
                float progressDifference = Mathf.Abs(currentFillAmount - targetValue);
                float dynamicFillSpeed = progressDifference * fillSpeed;
                loadingBar.fillAmount = Mathf.Lerp(currentFillAmount, targetValue, Time.deltaTime * dynamicFillSpeed);
            }
        }

        protected override void Start()
        {
            base.Start();

            // Reset state mỗi lần load scene
            isResourcesReady = false;
            targetProgress = 0f;
            isLoading = false;

            PDFSubjectService.OnReady += OnPDFServiceReady;
            PDFSubjectService.OnOverallProgress += OnPDFProgress;

            if (autoCheckOnStart && !hasStartedCheck)
            {
                hasStartedCheck = true;
                StartCoroutine(CheckResourcesCoroutine());
            }
        }

        private void OnDestroy()
        {
            PDFSubjectService.OnReady -= OnPDFServiceReady;
            PDFSubjectService.OnOverallProgress -= OnPDFProgress;
        }

        public bool AreResourcesReady() => isResourcesReady;

        public void StartResourceCheck()
        {
            if (isLoading || isResourcesReady) return;
            StartCoroutine(CheckResourcesCoroutine());
        }

        private IEnumerator CheckResourcesCoroutine()
        {
            isLoading = true;
            targetProgress = 0f;
            
            EnableLoadingCanvas(true);
            Log("Bắt đầu kiểm tra tài nguyên...");

            // Chờ PDFSubjectService instance (không timeout)
            while (PDFSubjectService.Instance == null)
            {
                yield return null;
            }

            Log("Đang tải tài nguyên...");

            // Fake progress từ 0 → 90% trong lúc chờ fetch
            float fakeProgressSpeed = 0.3f; // 30% per second
            float startTime = Time.time;
            
            // Chờ PDFSubjectService ready (không timeout - quy trình luôn chạy)
            while (!PDFSubjectService.IsReady)
            {
                // Smooth fake progress từ 0 → 0.9 (90%)
                float elapsedTime = Time.time - startTime;
                float fakeProgress = Mathf.Min(0.9f, elapsedTime * fakeProgressSpeed);
                
                // Combine actual progress từ PDFService (khi fetch xong, nó set = 1.0)
                targetProgress = Mathf.Max(fakeProgress, PDFSubjectService.OverallProgress);
                
                yield return null;
            }

            targetProgress = 1f;
            Log("Tài nguyên đã sẵn sàng!");
            
            // ALWAYS wait cho progress bar fill to 100% trước khi close canvas
            while (loadingBar != null && loadingBar.fillAmount < 0.99f)
            {
                yield return null;
            }
            
            yield return new WaitForSeconds(0.3f);
            
            MarkResourcesReady();
        }

        private void MarkResourcesReady()
        {
            Log("MarkResourcesReady called");
            isResourcesReady = true;
            isLoading = false;
            EnableLoadingCanvas(false);
            
            Log("Resources ready → SceneLoader có thể bắt đầu");
            Log($"[EVENT] Invoking OnResourcesReady event (subscribers: {OnResourcesReady?.GetInvocationList().Length ?? 0})");
            OnResourcesReady?.Invoke();
            Log($"[EVENT] OnResourcesReady event invoked completed");
        }

        private void EnableLoadingCanvas(bool enable)
        {
            isLoading = enable;
            
            if (loadingCanvas != null)
                loadingCanvas.gameObject.SetActive(enable);
            
            if (loadingCamera != null)
                loadingCamera.gameObject.SetActive(enable);

            if (enable && loadingBar != null)
            {
                loadingBar.fillAmount = 0f;
                targetProgress = 0f;
            }
        }

        private void OnPDFServiceReady()
        {
            Log("PDFSubjectService ready");
            targetProgress = 1f;
        }

        private void OnPDFProgress(float progress)
        {
            if (isLoading)
            {
                targetProgress = progress;
                OnDownloadProgress?.Invoke(progress);
            }
        }

        private void Log(string message)
        {
            if (enableDebugLog)
                Debug.Log($"[ResourceManager] {message}");
        }
    }
}
