using System.Threading.Tasks;

namespace NPCCore.Voiceline {
    public interface ICharacterVoiceline {
        Task PlayAnimation( string voiceKey , bool disableLoop);
    }
}
