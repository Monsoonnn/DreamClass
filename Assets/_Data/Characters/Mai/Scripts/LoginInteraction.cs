
using com.cyborgAssets.inspectorButtonPro;
using NPCCore.Voiceline;
using System.Threading.Tasks;
using TextToSpeech;
using UnityEngine;

namespace Characters.Mai
{
    public enum MaiVoiceType {
        success,
        login,
        fail,
        mutiFail,
    }
    public class LoginInteraction : VoicelineCtrl<MaiVoiceType> {


        public override async Task PlayAnimation( MaiVoiceType voiceType, bool BackStartGroup = true ) {
            /*Debug.Log($"[Mai] Playing voice: {voiceType}");*/
            await base.PlayAnimation(voiceType, BackStartGroup);
        }


    }

}