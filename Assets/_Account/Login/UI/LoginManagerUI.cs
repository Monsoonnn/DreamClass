using UnityEngine;
using System.Collections;

namespace DreamClass.LoginManager {
    public class LoginManagerUI : NewMonobehavior {
        public LoginUI loginUI;
        public GameObject registerUI;
        public NotificationUI notificationUI;

        // Show notification with message and duration
        public void ShowNotification( string message, float duration, GameObject thisObject ) {
            StartCoroutine(ShowNotificationRoutine(message, duration, thisObject));
        }

        public void ShowNotification( string message, GameObject thisObject ) {
            thisObject.SetActive(false);
            notificationUI.gameObject.SetActive(true);
            notificationUI.Show(message);
        }


        public void BackToLogin( GameObject thisObject ) {
            thisObject.SetActive(false);
            loginUI.gameObject.SetActive(true);
        }

        private IEnumerator ShowNotificationRoutine( string message, float duration, GameObject thisObject ) {

            // Activate notification and show message
            thisObject.SetActive(false);
            notificationUI.gameObject.SetActive(true);
            notificationUI.Show(message);

            // Wait for the duration
            yield return new WaitForSeconds(duration);

            // Hide message
            thisObject.SetActive(true);
            notificationUI.Hide();
            notificationUI.gameObject.SetActive(false);


           
        }
    }
}