using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using System.Collections;

namespace NPCCore.Animation {
    public class AnimationManager : NewMonobehavior {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationSetSO animationSet;

        [Header("Selections")]
        [SerializeField] private int selectedGroupIndex = 0;
        [SerializeField] private int selectedLayerIndex = 0;
        [SerializeField] private bool playOnStart = false;

        private Coroutine backRoutine;

        protected override void Start() {
            if (playOnStart)
                PlaySelectedGroup();
        }

        [ProButton]
        public void PlaySelectedGroup() {
            if (!ValidateSet()) return;
            var group = animationSet.GetGroupByIndex(selectedGroupIndex);
            if (group == null) return;
            PlayGroup(group);
        }

        [ProButton]
        public void PlaySelectedLayer( bool DisableLoop = true) {
            if (!ValidateSet()) return;
            var group = animationSet.GetGroupByIndex(selectedGroupIndex);
            if (group == null || group.layerAnimations.Count == 0) return;

            if (selectedLayerIndex < 0 || selectedLayerIndex >= group.layerAnimations.Count) {
                Debug.LogWarning("Invalid layer animation index!");
                return;
            }

            var layerAnim = group.layerAnimations[selectedLayerIndex];
            int layerIndex = animator.GetLayerIndex(layerAnim.layer.ToString());
            if (layerIndex < 0) layerIndex = 0;

            animator.CrossFade(layerAnim.animationName, layerAnim.transitionTime, layerIndex);
            Debug.Log($"Playing single animation: {layerAnim.animationName} ({layerAnim.layer})");

            if (backRoutine != null)
                StopCoroutine(backRoutine);

            if (DisableLoop && group.layerAnimations.Count > 0) {
                backRoutine = StartCoroutine(BackToIdleAfter(layerAnim, group, layerIndex));
            }
        }

        private IEnumerator BackToIdleAfter( AnimationSetSO.LayerAnimation layerAnim, AnimationSetSO.AnimationGroup group, int layerIndex ) {
            var clip = FindClipByName(layerAnim.animationName);
            if (clip == null) yield break;

            yield return new WaitForSeconds(clip.length);

            // Nếu layer là Base → quay về animation đầu tiên
            // Nếu layer khác Base → quay về animation "Empty"
            if (layerAnim.layer == AnimationSetSO.AnimationLayer.Base) {
                var idleAnim = group.layerAnimations[0];
                animator.CrossFade(idleAnim.animationName, idleAnim.transitionTime, layerIndex);
                Debug.Log($"Returned to idle (Base): {idleAnim.animationName}");
            } else {
                animator.CrossFade("Empty", 0.2f, layerIndex);
                Debug.Log($"Returned to Empty (Layer: {layerAnim.layer})");
            }

            backRoutine = null;
        }

        private AnimationClip FindClipByName( string animName ) {
            if (animator == null || animator.runtimeAnimatorController == null) return null;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == animName) return clip;
            return null;
        }

        private void PlayGroup( AnimationSetSO.AnimationGroup group ) {
            foreach (var layerAnim in group.layerAnimations) {
                int layerIndex = animator.GetLayerIndex(layerAnim.layer.ToString());
                if (layerIndex < 0) layerIndex = 0;
                animator.CrossFade(layerAnim.animationName, layerAnim.transitionTime, layerIndex);
            }
            Debug.Log($"Playing group: {group.groupName}");
        }

        private bool ValidateSet() {
            if (animator == null || animationSet == null) {
                Debug.LogWarning("Animator or AnimationSetSO is missing!");
                return false;
            }
            return true;
        }
    }
}
