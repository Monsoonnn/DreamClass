using Oculus.Interaction;
using Oculus.Interaction.OVR;
using UnityEngine;

public class HandInteractioAFKDetector : MonoBehaviour {
    public OVRHand leftHand;
    public OVRHand rightHand;
    public float afkThreshold = 300f;

    private float prevLeftScale;
    private float prevRightScale;

    private float timeSinceLastChange = 0f;
    

    void Start() {
        if (leftHand != null) prevLeftScale = leftHand.HandScale;
        if (rightHand != null) prevRightScale = rightHand.HandScale;
    }

    void Update() {
        bool hasChanged = CheckHandScaleChange();

        if (hasChanged) {
            timeSinceLastChange = 0f;
        } else {
            timeSinceLastChange += Time.deltaTime;
            if (timeSinceLastChange >= afkThreshold) {
                Debug.Log("Hand scale has not changed for 45 seconds");
                _ = VoicelineCtrl.Instance.PlayAnimation(VoiceType.loseDirection);
                timeSinceLastChange = 0f;
            }
        }
    }

    private bool CheckHandScaleChange() {
        bool changed = false;

        if (leftHand != null) {
            float currentLeftScale = leftHand.HandScale;
            if (currentLeftScale != prevLeftScale) {
                Debug.Log("Left hand scale changed: " + currentLeftScale);
                prevLeftScale = currentLeftScale;
                changed = true;
            }
        }

        if (rightHand != null) {
            float currentRightScale = rightHand.HandScale;
            if (currentRightScale != prevRightScale) {
                Debug.Log("Right hand scale changed: " + currentRightScale);
                prevRightScale = currentRightScale;
                changed = true;
            }
        }

        return changed;
    }
}
