using System.Collections.Generic;
using UnityEngine;

namespace NPCCore.Animation {
    [CreateAssetMenu(fileName = "NewAnimationSet", menuName = "NPC/Animation Set", order = 1)]
    public class AnimationSetSO : ScriptableObject {

        public enum AnimationLayer {
            Base,
            Upper,
            Lower,
            Head,
            TwoHand,
            LeftHand,
            RightHand
        }

        [System.Serializable]
        public class LayerAnimation {
            public string animationName;
            public AnimationLayer layer = AnimationLayer.Base;
            [Range(0f, 2f)] public float transitionTime = 0.3f;
        }

        [System.Serializable]
        public class AnimationGroup {
            [Tooltip("Tên logic của nhóm animation (ví dụ: Idle, Talk, Walk...)")]
            public string groupName = "Idle";
            public List<LayerAnimation> layerAnimations = new List<LayerAnimation>();
        }

        [Header("List of Animation Groups")]
        public List<AnimationGroup> groups = new List<AnimationGroup>();

        /// <summary>
        /// Get animation group by name
        /// </summary>
        public AnimationGroup GetGroup( string name ) {
            return groups.Find(g => g.groupName == name);
        }

        /// <summary>
        /// Get animation group by index (safe)
        /// </summary>
        public AnimationGroup GetGroupByIndex( int index ) {
            if (index < 0 || index >= groups.Count) return null;
            return groups[index];
        }

        
    }
}
