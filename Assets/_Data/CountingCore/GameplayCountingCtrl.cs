using com.cyborgAssets.inspectorButtonPro;
using System.Collections;
using UnityEngine;

namespace CountingCore {
    public class GameplayCountingCtrl : NewMonobehavior {

        public GameObject corePrefab;
        public GameObject activeCorePrefab;
        public Transform holder;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadCorePrefab();
            this.LoadHolder();
        }

        protected virtual void LoadHolder() {
            if (holder != null) return;
            this.holder = this.transform.Find("Holder");
            Debug.Log(transform.name + ": LoadHolder: ", gameObject);
        }

        protected virtual void LoadCorePrefab() {
            if (corePrefab != null) return;
            this.corePrefab = this.transform.Find("CorePrefab").gameObject;
            Debug.Log(transform.name + ": LoadCorePrefab: ", gameObject);
        }

        [ProButton]
        public virtual void StartGame() {
            if (holder == null) return;

            int childCount = holder.childCount;

          
            if (childCount == 1) {
                Debug.LogWarning("Holder already has 1 child. Skipping spawn.");
                return;
            }

            
            if (childCount > 1) {
                for (int i = holder.childCount - 1; i >= 1; i--) {
                    Destroy(holder.GetChild(i).gameObject);
                }
            }

            if (activeCorePrefab != null) Destroy(activeCorePrefab);
            StartCoroutine(DelayedInstantiate());
        }

        [ProButton]
        public virtual void Restart() {
            if (holder == null) return;

            
            foreach (Transform child in holder) {
                Destroy(child.gameObject);
            }

            activeCorePrefab = null;
            StartCoroutine(DelayedInstantiate());
        }

        private IEnumerator DelayedInstantiate() {
            yield return new WaitForSeconds(1f); // Delay 1s

            if (holder.childCount == 1) {
                Debug.LogWarning("Spawn canceled: holder already has 1 child.");
                yield break;
            }

            GameObject newCore = Instantiate(corePrefab, holder);
            newCore.SetActive(true);
            activeCorePrefab = newCore;
        }
    }
}
