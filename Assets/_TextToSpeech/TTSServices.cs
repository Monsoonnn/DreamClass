using UnityEngine;

namespace TextToSpeech {
    [CreateAssetMenu(fileName = "TTSServices", menuName = "TextToSpeech/TTSServices")]
    public class TTSServices : ScriptableObject {
        [Header("FPT.AI API Settings")]
        public string APIKey = "";
        [Range(0.5f, 2f)]
        public float speed = 1f;
        public string format = "mp3";
    }

    namespace TextToSpeech {
        public enum FPTVoice {

            //Nu 
            BanMai,    
            ThuMinh,   
            MyAn,    
            
            // Nam
            GiaHuy,    
            MinhQuang  
        }
    }

}
