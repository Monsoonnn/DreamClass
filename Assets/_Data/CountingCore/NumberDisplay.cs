using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CountingCore {
    public class NumberDisplay : NewMonobehavior {
        [SerializeField] protected CountingManager countingManager;
        [SerializeField] protected TextMeshPro textMeshPro;

        public bool isFinished = false;

        private void FixedUpdate() {
            UpdateText();   
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadCountingManager();
            this.LoadTextMeshPro();
        }

        protected void UpdateText() { 
            if(isFinished) return;
            int Targetcount = this.countingManager.ObjectCounter.TargetCount;
            if (Targetcount <= 0) {
                this.textMeshPro.text = "";
                this.isFinished = true;
            } else this.textMeshPro.text = Targetcount.ToString();
        }

        protected virtual void LoadCountingManager() {
            if (this.countingManager != null) return;
            this.countingManager = transform.GetComponentInParent<CountingManager>();
            Debug.Log(transform.name + ": LoadCountingManager: ", gameObject);
        }

        protected virtual void LoadTextMeshPro() {
            if (this.textMeshPro != null) return;
            this.textMeshPro = transform.GetComponentInChildren<TextMeshPro>();
            Debug.Log(transform.name + ": LoadTextMeshPro: ", gameObject);
        }
    }
}