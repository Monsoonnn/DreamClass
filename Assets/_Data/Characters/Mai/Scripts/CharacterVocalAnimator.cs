
using com.cyborgAssets.inspectorButtonPro;
using NPCCore.Voiceline;
using System.Threading.Tasks;
using TextToSpeech;
using UnityEngine;

namespace Characters.Mai
{
/*    public enum MaiVoiceType {
        success,
        login,
        fail,
        mutiFail,
        Q001_Active,
        Q001_Option1,
        Q001_Option2,
        Q001_Option3,

    }*/


    public class MaiVocalAnimatorCtrl : VoicelineManager<MaiVoiceType>, ICharacterVoiceline {


        public override async Task PlayAnimation( MaiVoiceType voiceType, bool BackStartGroup = true ) {
            /*Debug.Log($"[Mai] Playing voice: {voiceType}");*/
            await base.PlayAnimation(voiceType, BackStartGroup);
        }


    }

}