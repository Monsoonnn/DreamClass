using UnityEngine;

namespace DrawingSystem {
    public class BusketInteractionCtrlUI : InteractionUICtrl {

        [SerializeField] protected ColorEnumClass ColorEnumClass;
        public GetColorBusketBtn getColorBusketBtn;


        protected override void LoadComponents() { 
            base.LoadComponents();
            if(getColorBusketBtn == null) getColorBusketBtn = canvas.GetComponentInChildren<GetColorBusketBtn>();
        }

        public ColorEnumClass GetColorEnumClass() { return ColorEnumClass; }


    }





}