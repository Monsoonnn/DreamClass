using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Net;
using TextToSpeech.TextToSpeech;

namespace TextToSpeech {
    public class FPTTTSHandler : MonoBehaviour {
        private const string API_URL = "https://api.fpt.ai/hmi/tts/v5";

        private void Awake() {
            // Bypass SSL validation (chỉ nên dùng trong Unity Editor / test)
            ServicePointManager.ServerCertificateValidationCallback = ( a, b, c, d ) => true;
        }

        public IEnumerator RequestTTS( string text, FPTVoice voice, TTSServices service, System.Action<AudioClip> onCompleted ) {
            if (string.IsNullOrEmpty(service.APIKey)) {
                Debug.LogError("<color=#FF4444>[FPTTTS] Missing API key!</color>");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(text)) {
                Debug.LogWarning("<color=#FFFF55>[FPTTTS] Empty text, skipping synthesize.</color>");
                yield break;
            }

            string voiceName = GetVoiceName(voice);

            Debug.Log($"<color=#00FFFF>[FPTTTS] Sending request to FPT.AI</color>");
            Debug.Log($"<color=#AAAAAA>→ Voice:</color> <b>{voiceName}</b>");
            Debug.Log($"<color=#AAAAAA>→ Text:</color> \"{text}\"");

            using (UnityWebRequest www = new UnityWebRequest(API_URL, "POST")) {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(text);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();

                www.SetRequestHeader("api-key", service.APIKey);
                www.SetRequestHeader("voice", voiceName);
                www.SetRequestHeader("speed", service.speed.ToString());
                www.SetRequestHeader("Content-Type", "text/plain");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"<color=#FF4444>[FPTTTS] Request failed:</color> {www.error}");
                    yield break;
                }

                string responseJson = www.downloadHandler.text.Trim();

                if (string.IsNullOrEmpty(responseJson)) {
                    Debug.LogError("<color=#FF4444>[FPTTTS] Empty response from server!</color>");
                    yield break;
                }

                Debug.Log($"<color=#55FFFF>[FPTTTS] JSON Response:</color> {responseJson}");

                FPTResponse response = JsonUtility.FromJson<FPTResponse>(responseJson);

                if (response == null || string.IsNullOrEmpty(response.async)) {
                    Debug.LogError("<color=#FF4444>[FPTTTS] Invalid JSON response (no async link)!</color>");
                    yield break;
                }

                // ✅ Thay vì WaitForSeconds — tự kiểm tra cho đến khi file mp3 sẵn sàng
                yield return StartCoroutine(WaitForAudioReady(response.async, 12f)); // timeout 12 giây

                // Khi file sẵn sàng, tải và phát
                yield return StartCoroutine(LoadAudioClip(response.async, onCompleted));
            }
        }

        /// <summary>
        /// Gửi HEAD request để kiểm tra file đã tồn tại chưa. 
        /// Lặp lại cho đến khi nhận 200 OK hoặc hết thời gian timeout.
        /// </summary>
        private IEnumerator WaitForAudioReady( string url, float timeout ) {
            float elapsed = 0f;
            bool ready = false;

            Debug.Log("<color=#AAAAAA>[FPTTTS] Waiting for FPT server to generate audio...</color>");

            while (elapsed < timeout) {
                using (UnityWebRequest head = UnityWebRequest.Head(url)) {
                    yield return head.SendWebRequest();

                    if (head.result == UnityWebRequest.Result.Success && head.responseCode == 200) {
                        ready = true;
                        Debug.Log("<color=#55FF55>[FPTTTS] Audio file is ready!</color>");
                        break;
                    }
                }

                yield return new WaitForSeconds(1f);
                elapsed += 1f;
            }

            if (!ready) {
                Debug.LogWarning("<color=#FFAA00>[FPTTTS] Timeout waiting for FPT audio file — continuing anyway.</color>");
            }
        }

        private IEnumerator LoadAudioClip( string url, System.Action<AudioClip> onCompleted ) {
            Debug.Log($"<color=#AAAAAA>[FPTTTS] Downloading audio from:</color> {url}");

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG)) {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success) {
                    Debug.LogError($"<color=#FF4444>[FPTTTS] Load audio failed:</color> {www.error}");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip == null) {
                    Debug.LogError("<color=#FF4444>[FPTTTS] Null AudioClip returned!</color>");
                    yield break;
                }

                Debug.Log("<color=#55FF55>[FPTTTS] AudioClip loaded successfully!</color>");
                onCompleted?.Invoke(clip);
            }
        }

        private string GetVoiceName( FPTVoice voice ) {
            switch (voice) {
                case FPTVoice.BanMai: return "banmai";
                case FPTVoice.ThuMinh: return "thuminh";
                case FPTVoice.MyAn: return "myan";
                case FPTVoice.GiaHuy: return "giahuy";
                case FPTVoice.MinhQuang: return "minhquang";
                default: return "banmai";
            }
        }

        [System.Serializable]
        private class FPTResponse {
            public string async;
            public int error;
            public string message;
            public string request_id;
        }
    }
}
