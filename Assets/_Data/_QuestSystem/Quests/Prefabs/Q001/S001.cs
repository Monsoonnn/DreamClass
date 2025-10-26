
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace DreamClass.QuestSystem.Q001
{
    public class S001 : QuestStep
    {
        public GameObject optionUI;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadOptionUI();
        }

        protected virtual void LoadOptionUI()
        {
            if(optionUI != null) return;
            optionUI = transform.Find("OptionUI").gameObject;
        }

        [ProButton]
        protected virtual void SpawnOptionUI()
        {
            if (optionUI == null) return;

            GameObject clone = Instantiate(optionUI, transform);

            clone.gameObject.SetActive(true);

            questCtrl.transform.parent.gameObject.SetActive(false);
        }

        [ProButton]
        public override void OnUpdate(object context)
        {
            OnComplete();
            questCtrl.transform.parent.parent.gameObject.SetActive(true);
        }
    }
}