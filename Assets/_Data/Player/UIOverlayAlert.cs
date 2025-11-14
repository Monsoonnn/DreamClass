using UnityEngine;
using System.Collections.Generic;
using TMPro;
using com.cyborgAssets.inspectorButtonPro;
using NPOI.SS.Formula.Functions;

namespace playerCtrl
{
    public class VRAlertInstance : SingletonCtrl<VRAlertInstance>
    {
        [Header("Alert Settings")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descriptionText;
        public GameObject alertPrefab;      // Prefab cảnh báo
        public Transform alertParent;       // Vị trí spawn
        public int maxAlerts = 3;           // Giới hạn tối đa
        public float autoRemoveDelay = 3f;  // Thời gian tự xóa

        private readonly List<GameObject> activeAlerts = new List<GameObject>();


        private string baseTitle = "Lưu ý";
        private string questDescription = "Cần hoàn thiện các nhiệm vụ sau";

        protected override void Awake() {
            base.Awake();
            this.gameObject.SetActive(false);

        }

        /// <summary>
        /// Sinh danh sách cảnh báo từ list tên nhiệm vụ, tự động xóa sau delay.
        /// </summary>
        [ProButton]
        public void CreateAlerts(List<string> questNames)
        {
            if (questNames == null || questNames.Count == 0)
                return;

            if (titleText != null) titleText.text = baseTitle;
            if (descriptionText != null) descriptionText.text = questDescription;

            foreach (string questName in questNames)
            {
                if (activeAlerts.Count >= maxAlerts)
                {
                    Debug.LogWarning("VRAlertInstance: Alert limit reached!");
                    break;
                }

                GameObject newAlert = Instantiate(alertPrefab, alertParent ? alertParent : transform);
                newAlert.name = "VRAlert_" + questName;
                newAlert.GetComponentInChildren<TextMeshProUGUI>().text = questName;
                newAlert.SetActive(true);

                activeAlerts.Add(newAlert);

                // Dùng Invoke để tự xóa sau autoRemoveDelay giây
                Invoke(nameof(RemoveLastAlert), autoRemoveDelay);
            }

            gameObject.SetActive(activeAlerts.Count > 0);
        }

        public void CreateAlerts(List<string> questNames, string title, string description)
        {
            if (questNames == null || questNames.Count == 0)
                return;

            if (titleText != null) titleText.text = title;
            if (descriptionText != null) descriptionText.text = description;

            foreach (string questName in questNames)
            {
                if (activeAlerts.Count >= maxAlerts)
                {
                    Debug.LogWarning("VRAlertInstance: Alert limit reached!");
                    break;
                }

                GameObject newAlert = Instantiate(alertPrefab, alertParent ? alertParent : transform);
                newAlert.name = "VRAlert_" + questName;
                newAlert.GetComponentInChildren<TextMeshProUGUI>().text = questName;
                newAlert.SetActive(true);

                activeAlerts.Add(newAlert);

                // Dùng Invoke để tự xóa sau autoRemoveDelay giây
                Invoke(nameof(RemoveLastAlert), autoRemoveDelay);
            }

            gameObject.SetActive(activeAlerts.Count > 0);
        }

        // Hàm xóa alert đầu tiên (hoặc cũ nhất) còn tồn tại
        private void RemoveLastAlert()
        {
            if (activeAlerts.Count == 0)
                return;

            var alert = activeAlerts[0];
            activeAlerts.RemoveAt(0);
            if (alert != null)
                Destroy(alert);

            gameObject.SetActive(activeAlerts.Count > 0);
        }
    }
}
