using UnityEngine;

namespace DreamClass.LoginManager {
    [CreateAssetMenu(fileName = "Config", menuName = "Login/Config")]
    public class ConfigSO : ScriptableObject {
        [Header("Server Configuration")]
        public string hostURL = "http://localhost:3000"; 
    }
}
