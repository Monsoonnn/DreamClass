using NPCCore.Animation;
using NPCCore.Voiceline;
using UnityEngine;

namespace DreamClass.NPCCore {
    /// <summary>
    /// Generic NPCManager cho phép dùng bất kỳ loại VoicelineManager nào
    /// Thay vì phải tạo script con cho mỗi NPC
    /// </summary>
    public class NPCManagerGeneric<T> : NPCManager where T : ICharacterVoiceline {
        public T characterVoiceline;

        protected override void LoadComponents() {
            base.LoadComponents();
            
            // Nếu chưa assign, tự động tìm component T trên GameObject
            if (characterVoiceline == null) {
                characterVoiceline = GetComponent<T>();
            }
            
            if (characterVoiceline != null) {
                CharacterVoiceline = characterVoiceline;
            }
        }
    }
}
