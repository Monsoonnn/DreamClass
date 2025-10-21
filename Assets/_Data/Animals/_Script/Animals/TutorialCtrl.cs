using com.cyborgAssets.inspectorButtonPro;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

namespace GrabQuiz.Animals {
    public class TutorialCtrl : NewMonobehavior {
        public VoicelineCtrl voicelineCtrl;
        public PlayableDirector playableDirector;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadPlayableDirector();
            this.LoadVoiceline();
        }

        protected virtual void LoadVoiceline() { 
            if(this.voicelineCtrl != null) return;
            this.voicelineCtrl = GameObject.FindAnyObjectByType<VoicelineCtrl>();
            Debug.Log(transform.name + " LoadVoiceline: " + this.voicelineCtrl);
        }


        protected virtual void LoadPlayableDirector() { 
            if(this.playableDirector != null) return;
            this.playableDirector = this.GetComponent<PlayableDirector>();
            Debug.Log(transform.name + " LoadPlayableDirector: " + this.playableDirector);
        }

        [ProButton]
        public async Task StartTutorial() {

            await voicelineCtrl.PlayAnimation(BaseVoiceType.tutorial);

            this.playableDirector.Play(); 
        }

        public void EndTutorial() {

            _ = voicelineCtrl.PlayAnimation(BaseVoiceType.tutorialAnswer);
        }

    }
}
