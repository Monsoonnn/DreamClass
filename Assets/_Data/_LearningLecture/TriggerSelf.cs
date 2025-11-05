using UnityEngine;

namespace DreamClass.LearningLecture
{

    public class OVRButtonToggleSelf : OVRButtonHandlerBase
    {
        [Header("Toggle Settings")]
        public bool toggleOnPress = true;
        public GameObject menu;

        protected override void OnButtonPressed()
        {
            if (toggleOnPress)
            {
                //Debug.Log("Toggle " + gameObject.name);
                menu.gameObject.SetActive(!menu.activeSelf);
            }
        }
    }

}