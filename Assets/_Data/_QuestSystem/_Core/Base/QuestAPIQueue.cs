using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DreamClass.Network;

namespace DreamClass.QuestSystem
{
    /// <summary>
    /// Quản lý Queue các API request cho quest
    /// Đảm bảo các request được xử lý theo thứ tự FIFO
    /// </summary>
    public class QuestAPIQueue : SingletonCtrl<QuestAPIQueue>
    {
        [System.Serializable]
        private class QueuedRequest
        {
            public string requestId;
            public ApiRequest request;
            public Action<ApiResponse> callback;
            public float timeout = 10f;
            public float elapsedTime = 0f;
        }

        private Queue<QueuedRequest> requestQueue = new Queue<QueuedRequest>();
        private bool isProcessing = false;
        private QueuedRequest currentRequest = null;

        [Header("Debug")]
        [SerializeField] private bool logRequests = true;
        public int QueueCount => requestQueue.Count;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Thêm request vào queue
        /// </summary>
        public void EnqueueRequest(string requestId, ApiRequest request, Action<ApiResponse> callback, float timeout = 10f)
        {
            var queued = new QueuedRequest
            {
                requestId = requestId,
                request = request,
                callback = callback,
                timeout = timeout,
                elapsedTime = 0f
            };

            requestQueue.Enqueue(queued);
            if (logRequests)
                Debug.Log($"[QuestAPIQueue] Request enqueued: {requestId}. Queue size: {requestQueue.Count}");

            // Bắt đầu xử lý nếu chưa có gì đang xử lý
            if (!isProcessing)
            {
                StartCoroutine(ProcessQueue());
            }
        }

        /// <summary>
        /// Xử lý queue theo thứ tự
        /// </summary>
        private IEnumerator ProcessQueue()
        {
            isProcessing = true;

            while (requestQueue.Count > 0)
            {
                currentRequest = requestQueue.Dequeue();

                if (logRequests)
                    Debug.Log($"[QuestAPIQueue] Processing request: {currentRequest.requestId}. Remaining: {requestQueue.Count}");

                ApiClient apiClient = FindFirstObjectByType<ApiClient>();
                if (apiClient == null)
                {
                    Debug.LogError("[QuestAPIQueue] ApiClient not found!");
                    // Cannot create response without UnityWebRequest, skip this request
                    currentRequest.callback?.Invoke(null);
                    continue;
                }

                // Gửi request
                ApiResponse response = null;
                yield return apiClient.StartCoroutine(apiClient.SendRequest(currentRequest.request, r =>
                {
                    response = r;
                }));

                // Invoke callback
                currentRequest.callback?.Invoke(response);

                if (logRequests)
                    Debug.Log($"[QuestAPIQueue] Request completed: {currentRequest.requestId}. Success: {response?.IsSuccess}");

                // Delay một chút giữa các request để tránh quá tải server
                yield return new WaitForSeconds(0.1f);
            }

            isProcessing = false;
            currentRequest = null;

            if (logRequests)
                Debug.Log("[QuestAPIQueue] Queue processing completed");
        }

        /// <summary>
        /// Clear toàn bộ queue
        /// </summary>
        public void ClearQueue()
        {
            requestQueue.Clear();
            Debug.Log("[QuestAPIQueue] Queue cleared");
        }
    }
}
