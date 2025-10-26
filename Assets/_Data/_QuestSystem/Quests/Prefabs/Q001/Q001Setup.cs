using TMPro;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    public class Q001Setup : QuestSetup
    {

        public TMP_Text titleText;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadTitleText();
        }

        protected virtual void LoadTitleText()
        {
            if (titleText != null) return;
            titleText = this.transform.Find("Text")?.GetComponent<TMP_Text>();
            titleText.text = questCtrl.QuestName;
        }




    }
}