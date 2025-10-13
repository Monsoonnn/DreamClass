
using com.cyborgAssets.inspectorButtonPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DrawingSystem {

    public class PalleteCtrl : NewMonobehavior {
        public List<PalleteColor> palleteColors = new();


        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadPalleteColors();
        }


        protected virtual void LoadPalleteColors() { 
            if(palleteColors.Count > 0 && !this.palleteColors.Any(c=>c == null)) return;
            this.palleteColors = new List<PalleteColor>(GetComponentsInChildren<PalleteColor>());
            Debug.Log(transform.name + " : LoadPalleteColors: " + gameObject);
        }


        public virtual void RestartColorsByEnum(ColorEnumClass color) {

            foreach (var palleteColor in palleteColors) {
                if (palleteColor != null && palleteColor.ColorEnumClass == color) {
                    palleteColor.ResetSize();
                    return;
                }
            }


        }

        [ProButton]
        public virtual void DecreaseAllColors() {

            foreach (var palleteColor in palleteColors) {
                if (palleteColor != null) {
                    palleteColor.DecreaseSize();
                }
            }


        }

        [ProButton]
        public virtual void RestartAllColors() {

            foreach (var palleteColor in palleteColors) {
                if (palleteColor != null) {
                    palleteColor.ResetSize();
                }
            }


        }







    }



}
