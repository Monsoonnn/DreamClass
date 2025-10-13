using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;


namespace CountingCore {
    public class SuggetionCounter : MonoBehaviour {

        public int current = 0;
        public int TargetIndex = 3;


        [ProButton]
        public virtual bool UpdateSuggetion() {
            current++;
            if (current == TargetIndex) { 
                this.ResetSuggetion();
                _ = VoicelineCtrl.Instance.PlayAnimation(VoiceType.sugggetion);
                return true;
            }

            return false;
        }

        public virtual void ResetSuggetion() { current = 0; }


    } 
}