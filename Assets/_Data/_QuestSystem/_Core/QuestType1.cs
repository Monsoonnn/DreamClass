using UnityEngine;
namespace DreamClass.QuestSystem
{
    public abstract class QuestType1 : QuestCtrl
    {
        public Transform holdUI;

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

    }
}