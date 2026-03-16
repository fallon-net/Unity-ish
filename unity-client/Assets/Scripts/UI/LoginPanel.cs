using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityIsh.UI
{
    /// <summary>
    /// Simple login panel that collects credentials and invokes a callback.
    ///
    /// WIRING (Unity Inspector):
    ///   - UsernameField: TMP_InputField
    ///   - PasswordField: TMP_InputField (with ContentType = Password)
    ///   - LoginButton:   Button
    ///   - ErrorLabel:    TMP_Text (optional)
    ///
    /// The panel disables itself after a successful login.
    /// </summary>
    public sealed class LoginPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField usernameField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private Button loginButton;
        [SerializeField] private TMP_Text errorLabel;

        /// <summary>Raised when the user submits valid-looking credentials.</summary>
        public event Action<string, string> OnLoginSubmit;

        private void Awake()
        {
            if (errorLabel != null) errorLabel.gameObject.SetActive(false);
            loginButton.onClick.AddListener(HandleLoginClick);

            // Pre-fill username from last session if saved.
            string saved = PlayerPrefs.GetString("uis_last_user", "");
            if (!string.IsNullOrEmpty(saved) && usernameField != null)
                usernameField.text = saved;
        }

        private void HandleLoginClick()
        {
            string username = usernameField != null ? usernameField.text.Trim() : "";
            string password = passwordField != null ? passwordField.text : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Username and password are required.");
                return;
            }

            HideError();
            loginButton.interactable = false;
            PlayerPrefs.SetString("uis_last_user", username);
            OnLoginSubmit?.Invoke(username, password);
        }

        public void ShowError(string message)
        {
            if (errorLabel == null) return;
            errorLabel.text = message;
            errorLabel.gameObject.SetActive(true);
            loginButton.interactable = true;
        }

        public void HideError()
        {
            if (errorLabel == null) return;
            errorLabel.gameObject.SetActive(false);
        }

        public void Hide() => gameObject.SetActive(false);
        public void Show() => gameObject.SetActive(true);
    }
}
