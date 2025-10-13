using com.cyborgAssets.inspectorButtonPro;
using System.Collections.Generic;
using UnityEngine;

namespace DrawingSystem {
    public class GameplayDrawingGameCtrl : NewMonobehavior
    {
        [SerializeField] protected List<DrawingObjectCtrl> drawingObjects = new();

        public List<DrawingObjectCtrl> DrawingObjects => drawingObjects;

        public DrawingObjectCtrl activeDrawingObject;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadDrawingObjects();
        }

        protected virtual void LoadDrawingObjects() {
            if (this.drawingObjects.Count > 0) return;
            this.drawingObjects = new List<DrawingObjectCtrl>(GetComponentsInChildren<DrawingObjectCtrl>());
            Debug.Log(transform.name + " LoadDrawingObjects: " + this.drawingObjects.Count);
        }

        [ProButton]

        public virtual void StartNewGame() {
            if (drawingObjects.Count == 0) return;

            if (activeDrawingObject != null) {
                activeDrawingObject.ResetDrawingSpriteColor();
                activeDrawingObject.gameObject.SetActive(false);
                activeDrawingObject = null;
            } 

            int randomIndex = Random.Range(0, drawingObjects.Count);
            activeDrawingObject = drawingObjects[randomIndex];
            activeDrawingObject.gameObject.SetActive(true);
        }

        [ProButton]
        public virtual bool CheckingAnswer() {

            if (!activeDrawingObject.IsColorMatched()) return false;

            Debug.Log("CheckingAnswer: " + activeDrawingObject);

            return true;
        }
    }
}
