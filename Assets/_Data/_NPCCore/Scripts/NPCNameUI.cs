using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace DreamClass.NPCCore
{
    public class NPCNameUI : NewMonobehavior
    {
        public string NPCName;
        [SerializeField] private TMPro.TextMeshProUGUI nameText;

        protected override void Start()
        {
            nameText.text = NPCName;
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadTextMeshPro();
        }

        protected virtual void LoadTextMeshPro() { 
            if(nameText != null) return;
            nameText = GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }

        [ProButton]
        public void SetName(string newName) { nameText.text = newName; }


    }
}