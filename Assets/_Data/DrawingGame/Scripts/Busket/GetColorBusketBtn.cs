using UnityEngine;

namespace DrawingSystem {

    public class GetColorBusketBtn : ButtonAbstact {

        public PalleteCtrl pallete;
        public BusketInteractionCtrlUI busketInteractionCtrlUI;


        protected override void LoadComponents() {
            base.LoadComponents();
            if(busketInteractionCtrlUI == null) busketInteractionCtrlUI = this.transform.parent.parent.GetComponentInChildren<BusketInteractionCtrlUI>();
          /*  if(palleteCtrl == null) palleteCtrl = this.palleteCtrl = GameObject.FindAnyObjectByType<PalleteCtrl>();*/
        } 


        protected override void OnClick() {
            if (pallete == null || busketInteractionCtrlUI == null) { LoadComponents(); return; }

            Debug.Log(busketInteractionCtrlUI.GetColorEnumClass());

            pallete.RestartColorsByEnum(busketInteractionCtrlUI.GetColorEnumClass());
             
        }
    }


}
