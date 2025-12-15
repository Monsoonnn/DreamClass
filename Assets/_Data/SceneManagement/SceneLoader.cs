using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DreamClass.QuestSystem;
using DreamClass.QuestSystem.Systems.Quest;
using System.Linq;
using System.Reflection;
using playerCtrl;
using System.Collections.Generic;
using DreamClass.Locomotion;
using DreamClass.Subjects;
using DreamClass.ResourceGame;
using DreamClass.Audio;

namespace Systems.SceneManagement
{
    public class SceneLoader : MonoBehaviour, ISceneLoadNotifier
    {
        [SerializeField] Image loadingBar;
        [SerializeField] float fillSpeed = 0.5f;
        [SerializeField] Canvas loadingCanvas;
        [SerializeField] Camera loadingCamera;
        [SerializeField] SceneGroup[] sceneGroups;

        [Header("Services to wait for")]
        [Tooltip("Wait for PDFSubjectService to be ready before hiding loading screen")]
        [SerializeField] private bool waitForPDFService = true;
        [Tooltip("Wait for ResourceManager to confirm all resources are ready")]
        [SerializeField] private bool waitForResourceManager = true;

        public SceneGroup[] GetSceneGroups() => sceneGroups;

        // Event for ISceneLoadNotifier
        public event Action OnSceneLoadComplete;

        float targetProgress;
        bool isLoading;
        
        // Lock to prevent double loading
        private bool isLoadingScene = false;

        public readonly SceneGroupManager manager = new SceneGroupManager();

        void Awake()
        {
            // TODO can remove
            /*manager.OnSceneLoaded += sceneName => Debug.Log("Loaded: " + sceneName);
            manager.OnSceneUnloaded += sceneName => Debug.Log("Unloaded: " + sceneName);
            manager.OnSceneGroupLoaded += () => Debug.Log("Scene group loaded");*/
            
            // Pre-subscribe để tránh race condition khi ResourceManager invoke event
            Debug.Log("[SceneLoader] Awake: pre-subscribing to ResourceManager.OnResourcesReady");
            ResourceManager.OnResourcesReady += OnResourceManagerReady;
        }

        async void Start()
        {
            // Wait 1 frame to ensure all other Start() methods have run
            await Task.Yield();
            
            Debug.Log("[SceneLoader] Starting... waiting for ResourceManager");
            
            // LUÔN chờ ResourceManager xong trước khi vào game
            await WaitForResourceManagerReady();
            
            Debug.Log("[SceneLoader] ResourceManager done, starting scene load");
            
            // Sau khi ResourceManager xong, SceneLoader mới bắt đầu load scene
            await LoadSceneGroup(0);
        }

        /// <summary>
        /// Đợi ResourceManager kiểm tra và tải tài nguyên xong
        /// </summary>
        private bool resourcesReady = false;
        private TaskCompletionSource<bool> resourceReadyTcs = null;

        private void OnResourceManagerReady()
        {
            Debug.Log("[SceneLoader] OnResourceManagerReady callback triggered");
            resourcesReady = true;
            resourceReadyTcs?.TrySetResult(true);
        }

        private async Task WaitForResourceManagerReady()
        {
            Debug.Log("[SceneLoader] WaitForResourceManagerReady: checking if already ready...");
            
            // Nếu đã ready thì bỏ qua
            if (resourcesReady || ResourceManager.IsResourcesReady)
            {
                Debug.Log("[SceneLoader] ResourceManager already ready");
                return;
            }

            Debug.Log("[SceneLoader] ResourceManager not ready, waiting...");
            
            // Tạo TaskCompletionSource để wait
            resourceReadyTcs = new TaskCompletionSource<bool>();
            
            // Đợi event
            await resourceReadyTcs.Task;
            
            Debug.Log("[SceneLoader] *** Exiting WaitForResourceManagerReady - ResourceManager ready ***");
        }

        void Update()
        {
            if (!isLoading) return;

            float currentFillAmount = loadingBar.fillAmount;
            // IMPORTANT: Progress bar chỉ được tăng, KHÔNG bao giờ giảm
            float targetValue = Mathf.Max(currentFillAmount, targetProgress);
            
            float progressDifference = Mathf.Abs(currentFillAmount - targetValue);

            float dynamicFillSpeed = progressDifference * fillSpeed;

            loadingBar.fillAmount = Mathf.Lerp(currentFillAmount, targetValue, Time.deltaTime * dynamicFillSpeed);
        }

        public async Task LoadSceneGroup(int index)
        {
            // Prevent double loading
            if (isLoadingScene)
            {
                Debug.LogWarning("[SceneLoader] Already loading a scene, ignoring request");
                return;
            }

            if (index < 0 || index >= sceneGroups.Length)
            {
                Debug.LogError("Invalid scene group index: " + index);
                return;
            }

            isLoadingScene = true;
            loadingBar.fillAmount = 0f;
            targetProgress = 0f;

            // Hiện canvas của SceneLoader (sau khi ResourceManager đã ẩn)
            EnableLoadingCanvas();

            // Không cần đợi PDFService nữa vì ResourceManager đã lo
            // Bắt đầu load scene từ 0%
            targetProgress = 0f;

            // Load scenes (0% - 100%)
            LoadingProgress progress = new LoadingProgress();
            progress.Progressed += target => targetProgress = target;

            await manager.LoadScenes(sceneGroups[index], progress);

            targetProgress = 1f;

            RespawnPoint respawn = GameObject.FindAnyObjectByType<RespawnPoint>();
            if (respawn != null)
            {
                respawn.PlayerRespawn();
                Debug.Log($"[SceneLoader] Player respawned at {respawn.name}");
            }
            else
            {
                Debug.LogWarning("[SceneLoader] No RespawnPoint found in loaded scenes!");
            }

            EnableLoadingCanvas(false);
            isLoadingScene = false;

            // Notify listeners that the scene is fully loaded and ready.
            OnSceneLoadComplete?.Invoke();
        }

        public async Task LoadSceneGroup(string groupName)
        {
            // Prevent double loading
            if (isLoadingScene)
            {
                Debug.LogWarning("[SceneLoader] Already loading a scene, ignoring request");
                return;
            }

            int index = Array.FindIndex(sceneGroups, g => g.GroupName == groupName);

            if (index < 0)
            {
                Debug.LogError($"Scene group '{groupName}' not found!");
                return;
            }

            var group = sceneGroups[index];


            if (group.RequiredQuests != null && group.RequiredQuests.Count > 0)
            {
                if (!QuestPermissionManager.Instance.HasAll(group.RequiredQuests))
                {
                    List<string> missingQuests = group.RequiredQuests
                        .Where(q => !QuestPermissionManager.Instance.HasCompleted(q))
                        .ToList();

                    string missing = string.Join(", ", missingQuests);
                    Debug.LogWarning($"[SceneLoader] Access denied. Missing quests: {missing}");
                    List<string> questName = new List<string>();
                    if (QuestManager.Instance != null)
                    {
                        questName = QuestManager.Instance.GetQuestNames(missingQuests);
                    }
                    // Gửi cảnh báo VR
                    if (VRAlertInstance.Instance != null)
                    {
                        VRAlertInstance.Instance.CreateAlerts(questName);
                    }

                    return;
                }
                else
                {
                    Debug.Log($"[SceneLoader] Access granted for group '{groupName}'.");
                }
            }

            isLoadingScene = true;
            loadingBar.fillAmount = 0f;
            targetProgress = 0f;

            EnableLoadingCanvas();

            // Không cần đợi PDFService nữa vì ResourceManager đã lo
            targetProgress = 0f;

            // Load scenes (0% - 100%)
            LoadingProgress progress = new LoadingProgress();
            progress.Progressed += target => targetProgress = target;

            await manager.LoadScenes(sceneGroups[index], progress);

            targetProgress = 1f;

            RespawnPoint respawn = GameObject.FindAnyObjectByType<RespawnPoint>();
            if (respawn != null)
            {
                respawn.PlayerRespawn();
                Debug.Log($"[SceneLoader] Player respawned at {respawn.name}");
            }
            else
            {
                Debug.LogWarning("[SceneLoader] No RespawnPoint found in loaded scenes!");
            }

            EnableLoadingCanvas(false);
            isLoadingScene = false;

            // Notify listeners that the scene is fully loaded and ready.
            OnSceneLoadComplete?.Invoke();
        }



        void EnableLoadingCanvas(bool enable = true)
        {
            isLoading = enable;
            loadingCanvas.gameObject.SetActive(enable);
            loadingCamera.gameObject.SetActive(enable);

            /*            if (enable) {

                            Camera activeCam = Camera.main != null ? Camera.main : loadingCamera;


                            Vector3 forward = activeCam.transform.forward;
                            Vector3 position = activeCam.transform.position + forward * 2.25f;

                            loadingCanvas.transform.position = position;


                            loadingCanvas.transform.rotation = Quaternion.LookRotation(forward, activeCam.transform.up);
                        }*/
        }

    }

    public class LoadingProgress : IProgress<float>
    {
        public event Action<float> Progressed;

        const float ratio = 1f;

        public void Report(float value)
        {
            Progressed?.Invoke(value / ratio);
        }
    }
}