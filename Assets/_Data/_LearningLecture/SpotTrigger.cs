using DreamClass.LearningLecture;
using DreamClass.Lecture;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;

namespace DreamClass.LearningLecture
{
    [RequireComponent(typeof(BoxCollider))]
    public class SpotTrigger : NewMonobehavior
    {
        [SerializeField] private BoxCollider boxColider;
        [SerializeField] private GameObject spotTag;
        [SerializeField] private LearningModeManager learningModeManager;
        [SerializeField] private OVRButtonToggleSelf menuToggleSelf;

        [SerializeField] private List<GameObject> menuLectures;
        [SerializeField] private string triggerTag = "Player";

        private bool isInside = false;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadBoxCollider();
        }

        protected virtual void LoadBoxCollider()
        {
            if (this.boxColider != null) return;
            this.boxColider = GetComponent<BoxCollider>();
            this.boxColider.isTrigger = true;
            this.boxColider.center = new Vector3(0f, 0f, 0.5f);
            this.boxColider.size = new Vector3(2.5f, 5f, 3f);
            Debug.Log(transform.name + ": LoadBoxCollider", gameObject);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(triggerTag)) return;
            if (isInside) return;

            isInside = true;

            // Turn off spotTag
            if (spotTag.activeSelf)
                spotTag.SetActive(false);

            // Show menuLecture only once
            if (learningModeManager != null)
            {
                learningModeManager.ShowModeSelection();
            }

            // Enable menuToggleSelf
            if (menuToggleSelf != null)
            {
                menuToggleSelf.toggleOnPress = true;
                menuToggleSelf.gameObject.SetActive(true);
            }


        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(triggerTag)) return;
            if (!isInside) return;

            isInside = false;

            // Re-enable spotTag when leaving
            if (!spotTag.activeSelf)
                spotTag.SetActive(true);

            if (learningModeManager != null)
            {
                if (learningModeManager.currentMode == LearningModeManager.LearningMode.OnTap)
                {
                    learningModeManager.SetMode(LearningModeManager.LearningMode.None);
                }
                learningModeManager.HideAllPanels();
            }


            if (menuToggleSelf != null)
                menuToggleSelf.toggleOnPress = false;

            // Hide menuLectures on exit
            foreach (var menu in menuLectures)
            {
                if (menu != null)
                    menu.SetActive(false); // Hide the menu
            }
        }

    }
}
