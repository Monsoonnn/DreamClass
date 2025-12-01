using UnityEngine;
using DreamClass.NPCCore;

namespace Characters.Dung {
    public class ColiderDungLabScene : MonoBehaviour {

        [Header("References")]
        [SerializeField] private DungNPC dungNPC;

        [Header("Settings")]
        [SerializeField] private bool playOnce = true;
        
        private bool hasPlayed = false;

        private void Start()
        {
            if (dungNPC == null)
            {
                dungNPC = GetComponentInParent<DungNPC>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (playOnce && hasPlayed) return;
                
                PlayLabAsk();
                hasPlayed = true;
            }
        }

        private async void PlayLabAsk()
        {
            if (dungNPC == null)
            {
                Debug.LogWarning("[ColiderDungLabScene] DungNPC not found!");
                return;
            }

            Debug.Log("[ColiderDungLabScene] Player entered - Playing Lab_AskForExperiment");
            await dungNPC.characterVoiceline.PlayAnimation(DungVoiceType.Lab_AskForExperiment);
        }

        /// <summary>
        /// Reset để có thể play lại
        /// </summary>
        public void ResetTrigger()
        {
            hasPlayed = false;
        }
    }
}