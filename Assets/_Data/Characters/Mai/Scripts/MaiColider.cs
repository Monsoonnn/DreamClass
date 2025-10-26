using UnityEngine;

namespace Characters.Mai
{
    public class MaiCollider : NewMonobehavior
    {
        [SerializeField] private BoxCollider boxCollider;
        [SerializeField] private MaiVocalAnimatorCtrl loginInteraction;

        private bool hasGreeted;

        private void OnTriggerEnter(Collider other)
        {

            Debug.Log("OnTriggerEnter");
            // Only react to player objects
            if (!other.CompareTag("Player")) return;

            // Speak only the first time player enters
            if (hasGreeted) return;

            if (loginInteraction != null)
                _ = loginInteraction.PlayAnimation(MaiVoiceType.login);
            else
                Debug.LogWarning("[MaiCollider] Missing LoginInteraction reference.");

            hasGreeted = true;
        }
    }
}
