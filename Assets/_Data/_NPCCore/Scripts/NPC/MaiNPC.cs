
using Characters.Mai;
using NPCCore.Animation;
using NPCCore.Voiceline;
using UnityEngine;

namespace DreamClass.NPCCore
{
    public class MaiNPC : NPCManager { 
        public MaiVocalAnimatorCtrl characterVoiceline;

        protected override void LoadComponents() {
            base.LoadComponents();
            CharacterVoiceline = characterVoiceline; 
        }

    }
}