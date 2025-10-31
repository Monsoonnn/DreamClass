using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using com.cyborgAssets.inspectorButtonPro;
using System.Linq; // <-- Cần thêm thư viện này

namespace DreamClass.NPCCore {
    [CreateAssetMenu(fileName = "VoiceEnumSource", menuName = "DreamClass/Auto Enum Generator")]
    public class VoiceEnumSource : ScriptableObject {

        public string enumName = "DungVoiceType";
        public string namespaceName = "Characters.Mai";

        [Tooltip("Thư mục (relative to Assets) để lưu file enum được tạo ra.")]
        public string outputDirectory = "Assets/_Generated/Characters"; 

        public string[] values;

#if UNITY_EDITOR
        [ProButton]
        public void GenerateEnum() {
            // Kiểm tra xem tên enum có hợp lệ không
            if (string.IsNullOrWhiteSpace(enumName)) {
                Debug.LogError("Enum Name không được để trống.");
                return;
            }

            var sb = new StringBuilder();
            bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
            string indent = "";

            if (hasNamespace) {
                sb.AppendLine("namespace " + namespaceName + " {");
                indent = "    "; // 4 dấu cách
            }

            sb.AppendLine(indent + "public enum " + enumName + " {");
            foreach (var v in values) {
                // Làm sạch giá trị để đảm bảo nó là một định danh C# hợp lệ
                string validValue = SanitizeValue(v);
                if (!string.IsNullOrWhiteSpace(validValue)) {
                    sb.AppendLine(indent + "    " + validValue + ",");
                }
            }
            sb.AppendLine(indent + "}");

            if (hasNamespace) {
                sb.AppendLine("}");
            }

            // **PHẦN SỬA LỖI CHÍNH:**
            // 1. Tạo tên file từ enumName
            string fileName = enumName + ".cs";

            // 2. Kết hợp thư mục và tên file để có đường dẫn đầy đủ
            string fullPath = Path.Combine(outputDirectory, fileName);

            // 3. Tạo thư mục nếu nó chưa tồn tại
            Directory.CreateDirectory(outputDirectory);

            // 4. Ghi file vào đường dẫn động (fullPath)
            File.WriteAllText(fullPath, sb.ToString());

            AssetDatabase.Refresh();
            Debug.Log($"Enum '{enumName}' đã được tạo/cập nhật tại: {fullPath}");
        }

        /// <summary>
        /// Một hàm trợ giúp đơn giản để loại bỏ các ký tự không hợp lệ.
        /// </summary>
        private string SanitizeValue( string value ) {
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Loại bỏ khoảng trắng và các ký tự đặc biệt
            string sanitized = new string(value.ToCharArray()
                .Where(c => char.IsLetterOrDigit(c) || c == '_')
                .ToArray());

            // Đảm bảo nó không bắt đầu bằng số
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0])) {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }
#endif
    }
}