
using NPCCore.Animation;
using NPCCore.Voiceline;
using UnityEngine;

namespace DreamClass.NPCCore
{
    public abstract class NPCManager : NewMonobehavior
    {
        public Transform Model;
        public AnimationManager AnimationManager;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadModel();
        }

        protected void LoadModel() {
            if (Model != null) return;
            Model = transform.Find("Model");
        }
    }
}