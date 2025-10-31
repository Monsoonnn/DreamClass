using DreamClass.NPCCore;
using System.Threading.Tasks;
using UnityEngine;
namespace DreamClass.QuestSystem
{
    public class QuestType1 : QuestCtrl
    {
        public Transform holdUI;
        public NPCManager npcCtrl;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadHoldUI();
        }

        protected virtual void LoadHoldUI()
        {
            if(holdUI != null) return;
            holdUI = transform.parent.parent;
        }


        protected override Task CompleteQuest() {
            npcCtrl.Model.localRotation = Quaternion.identity;
            return base.CompleteQuest();

        }

    }
}