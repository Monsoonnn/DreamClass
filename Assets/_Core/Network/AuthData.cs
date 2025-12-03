using UnityEngine;
using System;
using System.Globalization;

namespace DreamClass.Network
{
    /// <summary>
    /// ScriptableObject để lưu trữ authentication data (Cookie hoặc JWT)
    /// </summary>
    [CreateAssetMenu(fileName = "AuthData", menuName = "DreamClass/Auth Data", order = 1)]
    public class AuthData : ScriptableObject
    {
        [Header("Authentication Type")]
        [SerializeField] private AuthType authType = AuthType.Cookie;
        
        [Header("Cookie Authentication")]
        [SerializeField] private string cookie = "";
        
        [Header("JWT Authentication")]
        [SerializeField] private string jwtToken = "";
        
        [Header("Expiration")]
        [SerializeField] private string expirationTime = ""; // ISO 8601 format
        [SerializeField] private bool enableExpirationCheck = true;
        
        public AuthType AuthType 
        { 
            get => authType; 
            set => authType = value; 
        }
        
        public string Cookie 
        { 
            get => cookie; 
            set => cookie = value; 
        }
        
        public string JwtToken 
        { 
            get => jwtToken; 
            set => jwtToken = value; 
        }
        
        /// <summary>
        /// Thời gian hết hạn của auth (ISO 8601 format)
        /// </summary>
        public string ExpirationTime
        {
            get => expirationTime;
            set => expirationTime = value;
        }
        
        /// <summary>
        /// Bật/tắt check expiration
        /// </summary>
        public bool EnableExpirationCheck
        {
            get => enableExpirationCheck;
            set => enableExpirationCheck = value;
        }
        
        /// <summary>
        /// Kiểm tra có authenticated không (bao gồm check expiration)
        /// </summary>
        public bool IsAuthenticated()
        {
            // Check expiration first
            if (enableExpirationCheck && IsExpired())
            {
                Debug.Log("[AuthData] Auth expired, clearing...");
                ClearAuth();
                return false;
            }
            
            switch (authType)
            {
                case AuthType.Cookie:
                    return !string.IsNullOrEmpty(cookie);
                case AuthType.JWT:
                    return !string.IsNullOrEmpty(jwtToken);
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Kiểm tra auth đã hết hạn chưa
        /// </summary>
        public bool IsExpired()
        {
            if (string.IsNullOrEmpty(expirationTime))
            {
                // Không có expiration time = không hết hạn (hoặc tùy logic)
                return false;
            }
            
            if (TryParseExpirationTime(expirationTime, out DateTime expTime))
            {
                bool expired = DateTime.UtcNow > expTime;
                if (expired)
                {
                    Debug.Log($"[AuthData] Auth expired at: {expTime:yyyy-MM-dd HH:mm:ss} UTC");
                }
                return expired;
            }
            
            // Không parse được = coi như không hết hạn
            Debug.LogWarning($"[AuthData] Cannot parse expiration time: {expirationTime}");
            return false;
        }
        
        /// <summary>
        /// Lấy thời gian còn lại trước khi hết hạn
        /// </summary>
        public TimeSpan GetTimeUntilExpiration()
        {
            if (string.IsNullOrEmpty(expirationTime))
            {
                return TimeSpan.MaxValue;
            }
            
            if (TryParseExpirationTime(expirationTime, out DateTime expTime))
            {
                var remaining = expTime - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            
            return TimeSpan.MaxValue;
        }
        
        /// <summary>
        /// Set expiration time từ DateTime
        /// </summary>
        public void SetExpirationTime(DateTime expTime)
        {
            expirationTime = expTime.ToUniversalTime().ToString("o"); // ISO 8601
            Debug.Log($"[AuthData] Expiration set to: {expirationTime}");
        }
        
        /// <summary>
        /// Set expiration time từ string (hỗ trợ nhiều format)
        /// </summary>
        public void SetExpirationTimeFromString(string expTimeStr)
        {
            if (TryParseExpirationTime(expTimeStr, out DateTime expTime))
            {
                SetExpirationTime(expTime);
            }
            else
            {
                // Lưu raw string nếu không parse được
                expirationTime = expTimeStr;
                Debug.LogWarning($"[AuthData] Stored raw expiration string: {expTimeStr}");
            }
        }
        
        /// <summary>
        /// Parse expiration time từ nhiều format khác nhau
        /// </summary>
        private bool TryParseExpirationTime(string timeStr, out DateTime result)
        {
            // Các format phổ biến
            string[] formats = new string[]
            {
                // ISO 8601
                "o",
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:ssZ",
                // HTTP date format (from Set-Cookie)
                "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                "ddd, dd-MMM-yyyy HH:mm:ss 'GMT'",
                // Other common formats
                "yyyy-MM-dd HH:mm:ss",
                "MM/dd/yyyy HH:mm:ss",
            };
            
            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(timeStr, format, CultureInfo.InvariantCulture, 
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
                {
                    return true;
                }
            }
            
            // Try generic parse
            if (DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, 
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return true;
            }
            
            result = DateTime.MinValue;
            return false;
        }
        
        /// <summary>
        /// Clear toàn bộ auth data
        /// </summary>
        public void ClearAuth()
        {
            cookie = "";
            jwtToken = "";
            expirationTime = "";
            Debug.Log("[AuthData] Auth data cleared");
        }
        
        /// <summary>
        /// Get auth value dựa trên type hiện tại
        /// </summary>
        public string GetAuthValue()
        {
            return authType == AuthType.Cookie ? cookie : jwtToken;
        }
    }
}
