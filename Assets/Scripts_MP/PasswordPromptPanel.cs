using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Password gate shown when joining a locked lobby. The user gets 3 attempts;
/// after the 3rd wrong try the prompt closes and they're back at the server list.
/// The actual hash comparison + StartClient happens in ServerPanelManager.ConfirmJoin,
/// which calls ShowError() back on us when the password is wrong.
/// </summary>
public class PasswordPromptPanel : MonoBehaviour
{
    [SerializeField] private GameObject     panel;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text       errorText;
    [SerializeField] private Button         confirmButton;
    [SerializeField] private Button         cancelButton;

    private const int MaxAttempts = 3;

    private int                  attempts;
    private CustomServerResponse target;
    private ServerPanelManager   manager;

    private void Awake()
    {
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton)  cancelButton.onClick.AddListener(Close);
        if (panel)         panel.SetActive(false);
    }

    public void Open(CustomServerResponse response, ServerPanelManager mgr)
    {
        target   = response;
        manager  = mgr;
        attempts = 0;

        // No password? Skip the prompt and join straight away.
        if (!response.HasPassword)
        {
            mgr.ConfirmJoin(response, "");
            return;
        }

        if (passwordInput) passwordInput.text = "";
        if (errorText)     errorText.gameObject.SetActive(false);
        if (panel)         panel.SetActive(true);
    }

    private void OnConfirm()
    {
        attempts++;
        // ConfirmJoin either joins (and Closes us) or calls ShowError() below.
        manager.ConfirmJoin(target, passwordInput ? passwordInput.text : "");
    }

    /// <summary>Called by ServerPanelManager when the entered password was wrong.</summary>
    public void ShowError(string message)
    {
        if (attempts >= MaxAttempts)
        {
            // Out of tries -> back to the server list.
            Close();
            return;
        }

        if (errorText)
        {
            errorText.text = $"{message} ({MaxAttempts - attempts} left)";
            errorText.gameObject.SetActive(true);
        }
        if (passwordInput) passwordInput.text = "";
    }

    public void Close()
    {
        if (panel) panel.SetActive(false);
    }
}
