using UnityEngine;

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
        /// Kiểm tra có authenticated không
        /// </summary>
        public bool IsAuthenticated()
        {
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
        /// Clear toàn bộ auth data
        /// </summary>
        public void ClearAuth()
        {
            cookie = "";
            jwtToken = "";
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
