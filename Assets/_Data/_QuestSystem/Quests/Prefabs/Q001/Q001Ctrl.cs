
using DreamClass.NPCCore;
using System.Threading.Tasks;
using UnityEngine;
namespace DreamClass.QuestSystem
{
    public class Q001Ctrl : QuestType1
    {
        public MaiNPC npcCtrl;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadNPCCtrl();
        }

        protected virtual void LoadNPCCtrl()
        {
            if (this.npcCtrl != null) return;
            this.npcCtrl = GameObject.FindAnyObjectByType<MaiNPC>();
        }

        protected override Task CompleteQuest() {
            npcCtrl.Model.transform.localRotation = Quaternion.identity;
            return base.CompleteQuest();

        }

    }
}