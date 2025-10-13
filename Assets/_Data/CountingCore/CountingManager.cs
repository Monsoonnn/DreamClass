using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CountingCore {
    public class CountingManager : NewMonobehavior {
        [SerializeField] protected ObjectCounter objectCounter;
        [SerializeField] protected NumberDisplay numberDisplay;
        public Image image;

        [Header("Basket Initialization")]
        public ItemGrabCode ItemGrabCode;
        public Sprite sprite;
        public ObjectCounter ObjectCounter => objectCounter;

        protected override void OnValidate() {
            base.OnValidate();
            this.ChangeImage();
        }


        protected void ChangeImage() { 
            if(image == null && sprite == null) return;
            this.image.sprite = sprite;
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadObjectCounter();
            this.LoadNumberDisplay();
            this.LoadImage();
        }

        protected virtual void LoadImage() { 
            if (this.image != null) return;
            this.image = transform.GetComponentInChildren<Image>();
            Debug.Log(transform.name + ": LoadImage: ", gameObject);    
        }


        protected virtual void LoadObjectCounter() { 
            if (this.objectCounter != null) return;
            this.objectCounter = transform.GetComponentInChildren<ObjectCounter>();
            Debug.Log(transform.name + ": LoadObjectCounter: ", gameObject);
        }

        protected virtual void LoadNumberDisplay() {
            if (this.numberDisplay != null) return;
            this.numberDisplay = transform.GetComponentInChildren<NumberDisplay>();
            Debug.Log(transform.name + ": LoadNumberDisplay", gameObject);
        }


        

    }
}
