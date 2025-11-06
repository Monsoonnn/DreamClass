using UnityEngine;
using Oculus.Interaction; // assuming ISDK_RayCanvasInteraction / ISDK_PokeCanvasInteraction are from Oculus SDK

namespace DreamClass.LearningLecture
{
    public class LearningBookCtrl : NewMonobehavior
    {
        [Header("References")]
        public GameObject bookObject;         // The 3D/VR Book
        public GameObject controlUIButton;    // UI buttons for controlling the book
        public GameObject tutorialUIObject;   // Tutorial screen with back button

        [Header("VR Canvas Interaction")]
        public GameObject rayCanvasInteraction;
        public GameObject pokeCanvasInteraction;

        private Vector3 bookPosition;
        private Quaternion bookRotation;


        public enum State
        {
            Hidden,
            Book,
            Tutorial
        }

        [Header("State Debug")]
        public State currentState = State.Hidden;

        protected override void Start()
        {
            ApplyState(currentState);
            bookPosition = bookObject.transform.position;
            bookRotation = bookObject.transform.rotation;
        }

        /// <summary>
        /// Switch the current state to State.Book, and apply the state to objects.
        /// This function will reset the position and rotation of the book object to
        /// the initial position and rotation stored in the bookTransform variable.
        /// It will also ensure that the Rigidbody component of the book object is set to
        /// kinematic, which means that physics will not be applied to the book object.
        /// </summary>
        public void SwitchToBookMode()
        {
            currentState = State.Book;
            ApplyState(currentState);
            if (bookObject != null)
            {

                // Reset position/rotation via Transform, không chạm velocity
                bookObject.transform.position = bookPosition;
                bookObject.transform.rotation = bookRotation;

            }
        }

        public void SwitchToTutorialMode()
        {
            currentState = State.Tutorial;
            ApplyState(currentState);
        }

        public void HideAll()
        {
            currentState = State.Hidden;
            ApplyState(currentState);
        }

        private void ApplyState(State state)
        {
            // Show/hide objects
            bool showBook = (state == State.Book);
            bool showControls = (state == State.Book);
            bool showTutorial = (state == State.Tutorial);

            if (bookObject != null) bookObject.SetActive(showBook);
            if (controlUIButton != null) controlUIButton.SetActive(showControls);
            if (tutorialUIObject != null) tutorialUIObject.SetActive(showTutorial);

            // Enable/disable VR canvas interaction
            bool enableInteraction = showControls || showTutorial;
            if (rayCanvasInteraction != null) rayCanvasInteraction.SetActive(enableInteraction);
            if (pokeCanvasInteraction != null) pokeCanvasInteraction.SetActive(enableInteraction);
        }
    }
}
