using System;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    public class QuestSetup: NewMonobehavior
    {
        public QuestCtrl questCtrl;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadQuestCtrl();
        }

        protected virtual void LoadQuestCtrl()
        {
            if (this.questCtrl != null) return;
            this.questCtrl = GetComponent<QuestCtrl>();
        }


    }
}