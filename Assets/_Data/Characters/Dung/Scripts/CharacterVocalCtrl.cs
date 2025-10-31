
using com.cyborgAssets.inspectorButtonPro;
using NPCCore.Voiceline;
using System.Threading.Tasks;
using TextToSpeech;
using UnityEngine;

namespace Characters.Dung {

    public class DungVocalAnimatorCtrl : VoicelineManager<DungVoiceType>, ICharacterVoiceline {


        public override async Task PlayAnimation( DungVoiceType voiceType, bool BackStartGroup = true ) {
            /*Debug.Log($"[Mai] Playing voice: {voiceType}");*/
            await base.PlayAnimation(voiceType, BackStartGroup);
        }


    }

}