using com.cyborgAssets.inspectorButtonPro;
using NPCCore.Voiceline;
using System.Threading.Tasks;
using TextToSpeech;
using UnityEngine;

namespace Characters.Quy
{
    // Enum QuyVoiceType nếu cần, bạn có thể bổ sung các loại voice cho Quy
    /*
    public enum QuyVoiceType {
        // ...
    }
    */

    public class QuyVocalCtrl : VoicelineManager<QuyVoiceType>, ICharacterVoiceline {
        public override async Task PlayAnimation(QuyVoiceType voiceType, bool BackStartGroup = true) {
            await base.PlayAnimation(voiceType, BackStartGroup);
        }
    }
}
