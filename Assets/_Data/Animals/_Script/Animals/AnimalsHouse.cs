using com.cyborgAssets.inspectorButtonPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrabQuiz.Animals {
    public class AnimalsHouse : NewMonobehavior {
        public List<AnimalCtrl> animalCtrlList = new List<AnimalCtrl>();

        [SerializeField] protected float spawnCycle = 30f;      // Chu ky chinh: 30s
        [SerializeField] protected float spawnDelay = 2f;       // Delay giua cac lan spawn trong chu ky
        [SerializeField] protected int maxSpawnPerCycle = 2;    // So lan toi da moi chu ky

        protected Coroutine respawnCoroutine;  // Dung de giu tham chieu den coroutine
        

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAnimalCtrlList();
        }

        protected virtual void LoadAnimalCtrlList() {
            if (this.animalCtrlList.Count > 0) return;
            this.animalCtrlList = new List<AnimalCtrl>(this.transform.GetComponentsInChildren<AnimalCtrl>());
            Debug.Log(transform.name + " LoadAnimalCtrlList: " + this.animalCtrlList.Count);
        }

        [ProButton]
        public void StartRespawnCycle() {
            
            if (respawnCoroutine != null) {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }

            respawnCoroutine = StartCoroutine(RespawnCycleRoutine());
        }

        [ProButton]
        public void StopRespawnCycle() {
            if (respawnCoroutine != null) {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }
        }

        protected IEnumerator RespawnCycleRoutine() {
            
            yield return StartCoroutine(RespawnWithinCycle());
        }

        protected IEnumerator RespawnWithinCycle() {
            for (int i = 0; i < maxSpawnPerCycle; i++) {
                yield return StartCoroutine(RespawnAnimalsWithDelay());
            }
        }

        private IEnumerator RespawnAnimalsWithDelay() {
            foreach (AnimalCtrl animal in animalCtrlList) {
                if (animal != null) {
                    animal.RespawnAnimals();
                    yield return new WaitForSeconds(spawnDelay);
                }
            }
            
            Debug.Log("Respawned animals at time: " + Time.time );
        }
    }
}
