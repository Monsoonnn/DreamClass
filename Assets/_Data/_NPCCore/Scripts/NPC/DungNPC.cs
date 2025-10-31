using Characters.Dung;
using DreamClass.NPCCore;
using UnityEngine;

namespace Dreamclass.NPCCore
{
    public class DungNPC : NPCManager
    {
       public DungVocalAnimatorCtrl characterVoiceline;
        protected override void LoadComponents() {
            base.LoadComponents();
            CharacterVoiceline = characterVoiceline;
        }
    }
}
