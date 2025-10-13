using com.cyborgAssets.inspectorButtonPro;
using System.Threading.Tasks;
using UnityEngine;

public enum VoiceType { 
    tutorial,
    idle,
    rightawnser,
    wrongawnser,   
    sugggetion,
    afk,
    loseDirection,
    tutorialAnswer,
    tutorialChangeRoom,
}


public class VoicelineCtrl : SingletonCtrl<VoicelineCtrl>
{
    public VoicelineAnimation[] voicelines;
    public Animator animator;
    public AudioSource audioSource;

    protected override void LoadComponents() {
        base.LoadComponents();
        if(animator == null) animator = this.transform.parent.Find("Ch31_nonPBR"). GetComponent<Animator>();
        if(audioSource == null) audioSource = GetComponent<AudioSource>();
    }


    [ProButton]
    public async Task PlayAnimation(VoiceType voiceType) {

        /*Debug.Log(voiceType.ToString());*/

        VoicelineAnimation playingVoiceline = GetVoiceline(voiceType);

        if (playingVoiceline != null)
        {
            await playingVoiceline.StartDialogue(audioSource, animator);
        }

    }

    protected virtual VoicelineAnimation GetVoiceline(VoiceType voiceType) {

        VoicelineAnimation item = null;


        foreach (VoicelineAnimation voiceline in voicelines) { 
            if(voiceline.voiceType == voiceType) item = voiceline ;
        }

        return item;
    }


}
