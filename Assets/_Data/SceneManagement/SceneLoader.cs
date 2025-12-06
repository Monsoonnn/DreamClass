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

namespace Systems.SceneManagement
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] Image loadingBar;
        [SerializeField] float fillSpeed = 0.5f;
        [SerializeField] Canvas loadingCanvas;
        [SerializeField] Camera loadingCamera;
        [SerializeField] SceneGroup[] sceneGroups;

        [Header("Services to wait for")]
        [Tooltip("Wait for PDFSubjectService to be ready before hiding loading screen")]
        [SerializeField] private bool waitForPDFService = true;
        [SerializeField] private float serviceTimeout = 30f; // Max wait time in seconds

        public SceneGroup[] GetSceneGroups() => sceneGroups;


        float targetProgress;
        bool isLoading;
        
        // Track if PDF service has been checked (only check once)
        private bool hasPDFServiceBeenChecked = false;
        
        // Lock to prevent double loading
        private bool isLoadingScene = false;

        public readonly SceneGroupManager manager = new SceneGroupManager();

        void Awake()
        {
            // TODO can remove
            /*manager.OnSceneLoaded += sceneName => Debug.Log("Loaded: " + sceneName);
            manager.OnSceneUnloaded += sceneName => Debug.Log("Unloaded: " + sceneName);
            manager.OnSceneGroupLoaded += () => Debug.Log("Scene group loaded");*/
        }

        async void Start()
        {
            // Wait 1 frame to ensure all other Start() methods have run (including PDFSubjectService)
            await Task.Yield();
            await LoadSceneGroup(0);
        }

        void Update()
        {
            if (!isLoading) return;

            float currentFillAmount = loadingBar.fillAmount;
            float progressDifference = Mathf.Abs(currentFillAmount - targetProgress);

            float dynamicFillSpeed = progressDifference * fillSpeed;

            loadingBar.fillAmount = Mathf.Lerp(currentFillAmount, targetProgress, Time.deltaTime * dynamicFillSpeed);
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

            EnableLoadingCanvas();

            // Step 1: Wait for PDFSubjectService to be ready (0% - 50%) - ONLY FIRST TIME
            if (waitForPDFService && !hasPDFServiceBeenChecked)
            {
                await WaitForServicesReady();
                hasPDFServiceBeenChecked = true;
            }
            else
            {
                targetProgress = 0.5f;
            }

            // Step 2: Load scenes (50% - 100%)
            LoadingProgress progress = new LoadingProgress();
            progress.Progressed += target => targetProgress = Mathf.Max(0.5f + (target * 0.5f), targetProgress);

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

            // Step 1: Wait for PDFSubjectService to be ready (0% - 50%) - ONLY FIRST TIME
            if (waitForPDFService && !hasPDFServiceBeenChecked)
            {
                await WaitForServicesReady();
                hasPDFServiceBeenChecked = true;
            }
            else
            {
                targetProgress = 0.5f;
            }

            // Step 2: Load scenes (50% - 100%)
            LoadingProgress progress = new LoadingProgress();
            progress.Progressed += target => targetProgress = Mathf.Max(0.5f + (target * 0.5f), targetProgress);

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
        }



        private async Task WaitForServicesReady()
        {
            Debug.Log("[SceneLoader] Waiting for PDFSubjectService...");
            
            float startTime = Time.time;
            
            // Step 1: Wait for PDFSubjectService.Instance to exist
            while (PDFSubjectService.Instance == null)
            {
                if (Time.time - startTime > serviceTimeout)
                {
                    Debug.LogWarning($"[SceneLoader] Timeout waiting for PDFSubjectService instance after {serviceTimeout}s");
                    targetProgress = 0.5f;
                    return;
                }
                await Task.Yield();
            }
            
            Debug.Log("[SceneLoader] PDFSubjectService instance found");

            // Step 2: Check if already ready
            if (PDFSubjectService.IsReady)
            {
                Debug.Log("[SceneLoader] PDFSubjectService already ready.");
                targetProgress = 0.5f;
                return;
            }

            Debug.Log("[SceneLoader] Waiting for PDFSubjectService to complete loading...");

            var tcs = new TaskCompletionSource<bool>();

            void OnServiceReady()
            {
                Debug.Log("[SceneLoader] Received OnReady event");
                tcs.TrySetResult(true);
            }

            void OnProgress(float progress)
            {
                // Update loading bar with PDF progress (0% - 50%)
                targetProgress = progress * 0.5f;
                if ((int)(progress * 100) % 10 == 0) // Log every 10%
                {
                    Debug.Log($"[SceneLoader] PDF loading progress: {(int)(progress * 100)}%");
                }
            }

            PDFSubjectService.OnReady += OnServiceReady;
            PDFSubjectService.OnOverallProgress += OnProgress;

            // Check with timeout - keep checking IsReady as backup
            while (!tcs.Task.IsCompleted)
            {
                if (PDFSubjectService.IsReady)
                {
                    Debug.Log("[SceneLoader] PDFSubjectService.IsReady detected");
                    tcs.TrySetResult(true);
                    break;
                }

                if (Time.time - startTime > serviceTimeout)
                {
                    Debug.LogWarning($"[SceneLoader] Timeout waiting for PDFSubjectService after {serviceTimeout}s");
                    tcs.TrySetResult(false);
                    break;
                }

                await Task.Yield();
            }

            PDFSubjectService.OnReady -= OnServiceReady;
            PDFSubjectService.OnOverallProgress -= OnProgress;
            
            targetProgress = 0.5f;
            Debug.Log("[SceneLoader] PDFSubjectService ready, continuing to load scene...");
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