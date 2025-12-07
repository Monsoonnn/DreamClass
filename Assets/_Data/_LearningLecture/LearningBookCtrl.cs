using System;
using System.Collections;
using UnityEngine;
using Oculus.Interaction; // assuming ISDK_RayCanvasInteraction / ISDK_PokeCanvasInteraction are from Oculus SDK
using DreamClass.Subjects;

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
        
        // Lazy loading coroutine tracking
        private Coroutine lazyLoadCoroutine;


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

        /// <summary>
        /// Start lazy loading coroutine (held by LearningBookCtrl which is always active)
        /// Called from BookPageLoader when user clicks to load a subject
        /// </summary>
        public void StartLazyLoadCoroutine(SubjectInfo subject, BookPageLoader pageLoader, Action<bool> callback, bool resetToFirstPage)
        {
            // Stop any existing lazy load
            if (lazyLoadCoroutine != null)
            {
                StopCoroutine(lazyLoadCoroutine);
                Debug.Log("[LearningBookCtrl] Stopped existing lazy load coroutine");
            }
            
            Debug.Log($"[LearningBookCtrl] Starting lazy load coroutine for {subject.name}");
            lazyLoadCoroutine = StartCoroutine(LazyLoadCoroutine(subject, pageLoader, callback, resetToFirstPage));
        }

        private IEnumerator LazyLoadCoroutine(SubjectInfo subject, BookPageLoader pageLoader, Action<bool> callback, bool resetToFirstPage)
        {
            bool loadComplete = false;
            Sprite[] loadedSprites = null;

            // Request sprites from PDFSubjectService
            // Note: LoadSubjectSpritesOnDemand only has success callback, no error callback
            PDFSubjectService.Instance.StartCoroutine(
                PDFSubjectService.Instance.LoadSubjectSpritesOnDemand(
                    subject.cloudinaryFolder,
                    (sprites) =>
                    {
                        loadedSprites = sprites;
                        loadComplete = true;
                        if (sprites != null && sprites.Length > 0)
                        {
                            Debug.Log($"[LearningBookCtrl] Lazy load completed: {sprites.Length} sprites for {subject.name}");
                        }
                        else
                        {
                            Debug.LogError($"[LearningBookCtrl] Lazy load returned no sprites for {subject.name}");
                        }
                    }
                )
            );

            // Wait for load to complete (with timeout)
            float timeout = 60f;
            float elapsedTime = 0f;
            while (!loadComplete && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;
            }

            if (!loadComplete)
            {
                Debug.LogError($"[LearningBookCtrl] Lazy load timeout after {timeout}s for {subject.name}");
                callback?.Invoke(false);
                yield break;
            }

            if (loadedSprites == null || loadedSprites.Length == 0)
            {
                Debug.LogError($"[LearningBookCtrl] Lazy load returned no sprites for {subject.name}");
                callback?.Invoke(false);
                yield break;
            }

            // Process loaded sprites through BookPageLoader
            bool success = pageLoader.ProcessLoadedSprites(subject, loadedSprites, resetToFirstPage);
            callback?.Invoke(success);
            
            lazyLoadCoroutine = null;
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
