using com.cyborgAssets.inspectorButtonPro;
using DrawingSystem;
using System.Collections.Generic;
using UnityEngine;

namespace DrawingSystem
{
    public class DrawingObjectCtrl : NewMonobehavior {
        public List<SpriteDrawingCanvas> listDrawingCanvas = new();
        public List<TargetDrawing> listTargetDrawing = new();

        protected override void Start() {
            this.gameObject.SetActive(false);
        }


        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadDrawingCanvas();
            this.LoadTargetDrawing();
        }

        protected virtual void LoadDrawingCanvas() {
            if (listDrawingCanvas.Count > 0) return;
            this.listDrawingCanvas = new List<SpriteDrawingCanvas>(this.transform.Find("DrawingObj").GetComponentsInChildren<SpriteDrawingCanvas>());
            Debug.Log(transform.name + " LoadDrawingCanvas " + listDrawingCanvas.Count);
        }

        protected virtual void LoadTargetDrawing() {
            if (listTargetDrawing.Count > 0) return;
            this.listTargetDrawing = new List<TargetDrawing>(this.transform.Find("TargetObj").GetComponentsInChildren<TargetDrawing>());
            Debug.Log(transform.name + " LoadTargetDrawing " + listDrawingCanvas.Count);
        }

        [ProButton]
        public virtual bool IsColorMatched() {
            int count = Mathf.Min(listDrawingCanvas.Count, listTargetDrawing.Count);

            for (int i = 0; i < count; i++) {
                Color drawingColor = listDrawingCanvas[i].ReturnDrawingColor();
                Color targetColor = listTargetDrawing[i].ReturnTargetColor();

                if (!IsSameColor(drawingColor, targetColor)) {
                    return false;
                }
            }

            return true;
        }

        [ProButton]

        public virtual void ResetDrawingSpriteColor() { 
            for (int i = 0; i < listDrawingCanvas.Count; i++) {
                listDrawingCanvas[i].image.color = Color.white;
            }
        }

        protected virtual bool IsSameColor( Color a, Color b, float tolerance = 0.01f ) {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance &&
                   Mathf.Abs(a.a - b.a) < tolerance;
        }

        public virtual bool IsColorMatchedAtIndex(int index) {
            if (index < 0 || index >= listDrawingCanvas.Count || index >= listTargetDrawing.Count) return false;

            Color drawingColor = listDrawingCanvas[index].ReturnDrawingColor();
            Color targetColor = listTargetDrawing[index].ReturnTargetColor();

            return IsSameColor(drawingColor, targetColor);
        }
    }
}