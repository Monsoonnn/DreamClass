using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using System.Collections;

namespace NPCCore.Animation {
    public class AnimationManager : NewMonobehavior {
        [Header("References")]
        public Animator animator;
        [SerializeField] private AnimationSetSO animationSet;

        [Header("Selections")]
        [SerializeField] private int selectedGroupIndex = 0;
        [SerializeField] private int selectedLayerIndex = 0;
        [SerializeField] private bool playOnStart = false;

        [Header("Return Settings")]
        [SerializeField] private bool returnToStartAnimation = false;

        private Coroutine backRoutine;
        private AnimationSetSO.AnimationGroup startGroup;

        protected override void Start() {
            if (!ValidateSet()) return;

            startGroup = animationSet.GetGroupByIndex(selectedGroupIndex);

            if (playOnStart && startGroup != null)
                PlayGroup(startGroup);
        }

        [ProButton]
        public void PlaySelectedGroup( bool disableLoop = false ) {
            if (!ValidateSet()) return;

            if (backRoutine != null) {
                StopCoroutine(backRoutine);
                backRoutine = null;
            }

            Debug.Log($"Disable Loop group: {disableLoop}");

            var group = animationSet.GetGroupByIndex(selectedGroupIndex);
            if (group == null || group.layerAnimations.Count == 0) {
                Debug.LogWarning("No valid animation group selected!");
                return;
            }

            PlayGroup(group);

            if (disableLoop)
                backRoutine = StartCoroutine(BackToIdleAfterGroup(group));

            Debug.Log($"Playing group: {group.groupName}");
        }

        private IEnumerator BackToIdleAfterGroup( AnimationSetSO.AnimationGroup group ) {
            float longestClip = 0f;

            // Find longest clip duration
            foreach (var layerAnim in group.layerAnimations) {
                var clip = FindClipByName(layerAnim.animationName);
                if (clip != null && clip.length > longestClip)
                    longestClip = clip.length;
            }

            Debug.Log($"Longest clip duration: {longestClip}");

            if (longestClip <= 0f)
                yield break;

            yield return new WaitForSeconds(longestClip);

            // Determine base and non-base layers
            int baseLayerIndex = animator.GetLayerIndex(AnimationSetSO.AnimationLayer.Base.ToString());
            if (baseLayerIndex < 0) baseLayerIndex = 0;

            // --- Base Layer ---
            if (returnToStartAnimation && startGroup != null) {
                var baseAnim = startGroup.GetAnimationByLayer(AnimationSetSO.AnimationLayer.Base);
                if (baseAnim != null)
                    animator.CrossFade(baseAnim.animationName, baseAnim.transitionTime, baseLayerIndex);
                Debug.Log($"Returned Base Layer to StartGroup: {startGroup.groupName}");
            } else {
                var idleGroup = animationSet.GetGroup("Idle");
                if (idleGroup != null) {
                    var idleAnim = idleGroup.GetAnimationByLayer(AnimationSetSO.AnimationLayer.Base);
                    if (idleAnim != null)
                        animator.CrossFade(idleAnim.animationName, idleAnim.transitionTime, baseLayerIndex);
                }
                Debug.Log("Returned Base Layer to Idle");
            }

            // --- Other Layers ---
            int totalLayers = animator.layerCount;
            for (int i = 0; i < totalLayers; i++) {
                if (i == baseLayerIndex) continue; // Skip base
                animator.CrossFade("empty", 0.2f, i);
                Debug.Log($"Cleared Layer {i} to Empty");
            }

            backRoutine = null;
        }

        private IEnumerator BackToIdleAfterGroup( AnimationSetSO.AnimationGroup group, float clipDuration ) {
            if (clipDuration > 0f)
                Debug.Log($"Longest clip duration: {clipDuration}");
            yield return new WaitForSeconds(clipDuration);




            yield return new WaitForSeconds(clipDuration);

            // Determine base and non-base layers
            int baseLayerIndex = animator.GetLayerIndex(AnimationSetSO.AnimationLayer.Base.ToString());
            if (baseLayerIndex < 0) baseLayerIndex = 0;

            // --- Base Layer ---
            if (returnToStartAnimation && startGroup != null) {
                var baseAnim = startGroup.GetAnimationByLayer(AnimationSetSO.AnimationLayer.Base);
                if (baseAnim != null)
                    animator.CrossFade(baseAnim.animationName, baseAnim.transitionTime, baseLayerIndex);
                Debug.Log($"Returned Base Layer to StartGroup: {startGroup.groupName}");
            } else {
                var idleGroup = animationSet.GetGroup("Idle");
                if (idleGroup != null) {
                    var idleAnim = idleGroup.GetAnimationByLayer(AnimationSetSO.AnimationLayer.Base);
                    if (idleAnim != null)
                        animator.CrossFade(idleAnim.animationName, idleAnim.transitionTime, baseLayerIndex);
                }
                Debug.Log("Returned Base Layer to Idle");
            }

            // --- Other Layers ---
            int totalLayers = animator.layerCount;
            for (int i = 0; i < totalLayers; i++) {
                if (i == baseLayerIndex) continue; // Skip base
                animator.CrossFade("empty", 0.2f, i);
                Debug.Log($"Cleared Layer {i} to Empty");
            }

            backRoutine = null;
        }

        [ProButton]
        public void PlaySelectedLayer( bool DisableLoop = true ) {
            if (!ValidateSet()) return;

            if (backRoutine != null) {
                StopCoroutine(backRoutine);
                backRoutine = null;
            }

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

            if (DisableLoop)
                backRoutine = StartCoroutine(BackToIdleAfter(layerAnim, group, layerIndex));
        }

        private IEnumerator BackToIdleAfter( AnimationSetSO.LayerAnimation layerAnim, AnimationSetSO.AnimationGroup group, int layerIndex ) {
            var clip = FindClipByName(layerAnim.animationName);
            if (clip == null) yield break;

            yield return new WaitForSeconds(clip.length);

            if (returnToStartAnimation && startGroup != null) {
                PlayGroup(startGroup);
                Debug.Log($"Returned to Start Animation: {startGroup.groupName}");
            } else {
                if (layerAnim.layer == AnimationSetSO.AnimationLayer.Base) {
                    var idleAnim = group.layerAnimations[0];
                    animator.CrossFade(idleAnim.animationName, idleAnim.transitionTime, layerIndex);
                    Debug.Log($"Returned to idle (Base): {idleAnim.animationName}");
                } else {
                    animator.CrossFade("Empty", 0.2f, layerIndex);
                    Debug.Log($"Returned to Empty (Layer: {layerAnim.layer})");
                }
            }

            backRoutine = null;
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

        public AnimationSetSO GetAnimationSet() => animationSet;

        public AnimationClip FindClipByName( string name ) {
            if (animator == null || animator.runtimeAnimatorController == null) return null;
            foreach (var clip in animator.runtimeAnimatorController.animationClips) {
                if (clip.name == name) return clip;
                Debug.Log($"AnimatorClip: {clip.name}");
            }
                
            return null;
        }

        public void PlayGroupByName( string groupName, bool disableLoop = false , float clipDuration = 0f) {
            var group = animationSet.GetGroup(groupName);
            if (group == null) {
                Debug.LogWarning($"Group '{groupName}' not found in AnimationSet!");
                return;
            }

            if (backRoutine != null) {
                StopCoroutine(backRoutine);
                backRoutine = null;
            }

            PlayGroup(group);

            if (disableLoop && clipDuration > 0f)
                backRoutine = StartCoroutine(BackToIdleAfterGroup(group, clipDuration - 1f));
            else if(disableLoop && clipDuration <= 0f)
                backRoutine = StartCoroutine(BackToIdleAfterGroup(group));

        }

        public void PlayIdle() {
            var idleGroup = animationSet.GetGroup("Idle");
            if (idleGroup != null)
                PlayGroup(idleGroup);
        }

        public void PlayStartGroup() { 
            this.PlayGroup(startGroup);
        }


    }
}
