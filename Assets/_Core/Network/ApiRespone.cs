using System.Text;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace DreamClass.Network {
    public class ApiResponse {
        public long StatusCode;
        public string Text;
        public string Error;
        public string SetCookie;

        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

        public ApiResponse( UnityWebRequest request ) {
            StatusCode = request.responseCode;
            Text = request.downloadHandler != null ? request.downloadHandler.text : "";
            Error = request.error;
            SetCookie = request.GetResponseHeader("Set-Cookie");

            // Only log JSON/text from server
            if (!string.IsNullOrEmpty(Text)) {
                Debug.Log($"[Server Response]\n{FormatJson(Text)}");
            }
        }

        private string FormatJson( string json ) {
            try {
                int indent = 0;
                bool quoted = false;
                var sb = new StringBuilder();
                for (int i = 0; i < json.Length; i++) {
                    char ch = json[i];
                    switch (ch) {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            if (!quoted) {
                                sb.AppendLine();
                                sb.Append(new string(' ', ++indent * 4));
                            }
                            break;
                        case '}':
                        case ']':
                            if (!quoted) {
                                sb.AppendLine();
                                sb.Append(new string(' ', --indent * 4));
                            }
                            sb.Append(ch);
                            break;
                        case '"':
                            sb.Append(ch);
                            bool escaped = false;
                            int index = i;
                            while (index > 0 && json[--index] == '\\')
                                escaped = !escaped;
                            if (!escaped)
                                quoted = !quoted;
                            break;
                        case ',':
                            sb.Append(ch);
                            if (!quoted) {
                                sb.AppendLine();
                                sb.Append(new string(' ', indent * 4));
                            }
                            break;
                        case ':':
                            sb.Append(ch);
                            if (!quoted) sb.Append(" ");
                            break;
                        default:
                            sb.Append(ch);
                            break;
                    }
                }
                return sb.ToString();
            }
            catch (Exception) {
                return json; // fallback if it's not valid JSON
            }
        }
    }
}
