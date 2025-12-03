using NPCCore.Voiceline;
using System.Threading.Tasks;
using UnityEngine;

namespace Characters.TeacherKien
{

    public enum ThayKienVoiceType {
        alertLogin,
        welcome,
        fail,
        guide,
        rankingGuide,
    }
    public class TeacherKienInteraction : VoicelineManager<ThayKienVoiceType> {

        public override async Task PlayAnimation( ThayKienVoiceType voiceType, bool BackStartGroup = true ) {
            /*Debug.Log($"[Mai] Playing voice: {voiceType}");*/
            await base.PlayAnimation(voiceType, BackStartGroup);
        }


    }
}