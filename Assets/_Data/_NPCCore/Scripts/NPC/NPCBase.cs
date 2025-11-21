using com.cyborgAssets.inspectorButtonPro;
using NPCCore.Voiceline;
using UnityEngine;

namespace DreamClass.NPCCore
{
    /// <summary>
    /// NPC Base - Tự động tìm kiếm bất kỳ component nào implement ICharacterVoiceline
    /// Không cần tạo riêng QuyNPC hay DungNPC nữa
    /// Chỉ cần kéo script này vào GameObject
    /// </summary>
    public class NPC : NPCManager
    {
        [SerializeField] private ICharacterVoiceline characterVoiceline;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            
            // Tự động tìm component implement ICharacterVoiceline
            if (characterVoiceline == null)
            {
                characterVoiceline = GetComponent<ICharacterVoiceline>();
            }
            
            // Nếu không tìm thấy trên GameObject hiện tại, tìm trong children
            if (characterVoiceline == null)
            {
                characterVoiceline = GetComponentInChildren<ICharacterVoiceline>();
            }
            
            if (characterVoiceline != null)
            {
                CharacterVoiceline = characterVoiceline;
            }
            else
            {
                Debug.LogWarning($"[NPC] Không tìm thấy component implement ICharacterVoiceline trên {gameObject.name}", gameObject);
            }
        }
    }
}
