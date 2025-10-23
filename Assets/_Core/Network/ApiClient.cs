using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

namespace DreamClass.Network {
    public class ApiClient : MonoBehaviour {
        [SerializeField] private string baseUrl;
        [SerializeField] private string defaultCookie;
        public string DefaultCookie => defaultCookie;
        public void SetBaseUrl( string url ) => baseUrl = url;
        public void SetCookie( string cookie ) => defaultCookie = cookie;

        public IEnumerator SendRequest( ApiRequest request, System.Action<ApiResponse> callback ) {
            string url = $"{baseUrl}{request.Endpoint}";
            UnityWebRequest webRequest = new UnityWebRequest(url, request.Method);

            // Set body
            if (!string.IsNullOrEmpty(request.Body)) {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(request.Body);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // Default headers
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(defaultCookie))
                webRequest.SetRequestHeader("Cookie", defaultCookie);

            // Custom headers (if any)
            if (request.Headers != null) {
                foreach (var kv in request.Headers)
                    webRequest.SetRequestHeader(kv.Key, kv.Value);
            }

            yield return webRequest.SendWebRequest();

            ApiResponse response = new ApiResponse(webRequest);
            callback?.Invoke(response);
        }
    }
}
