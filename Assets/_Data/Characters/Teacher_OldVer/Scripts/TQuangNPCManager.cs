using DreamClass.NPCCore;
using UnityEngine;

namespace Characters.TeacherQuang {
    public class TQuangNPCManager : NPCManager  {
        public TeacherQuangVocalAnimator characterVoiceline;

        protected override void LoadComponents() {
            base.LoadComponents();
            CharacterVoiceline = characterVoiceline; 
        }
    }
}
