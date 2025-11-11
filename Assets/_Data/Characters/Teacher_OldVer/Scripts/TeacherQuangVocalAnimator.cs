
using com.cyborgAssets.inspectorButtonPro;
using NPCCore.Voiceline;
using System.Threading.Tasks;
using TextToSpeech;
using UnityEngine;

namespace Characters.TeacherQuang
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


    public class TeacherQuangVocalAnimator : VoicelineManager<TeacherQuang>, ICharacterVoiceline {


        public override async Task PlayAnimation( TeacherQuang voiceType, bool BackStartGroup = true ) {
            /*Debug.Log($"[Mai] Playing voice: {voiceType}");*/
            await base.PlayAnimation(voiceType, BackStartGroup);
        }


    }

}