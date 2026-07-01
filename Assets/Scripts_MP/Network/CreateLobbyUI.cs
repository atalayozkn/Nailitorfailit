// ============================================================
// File: CreateLobbyUI.cs
// Author: Murad
// Created: 30-Jun-2026
// Purpose: Utility methods for returning the lobby password from the UI
// ============================================================

using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateLobbyUI : MonoBehaviour
{
    [SerializeField] TMP_InputField passwordInput;
    [SerializeField] Button createButton;
    [SerializeField] TMP_Text statusText;

    void Awake() => createButton.onClick.AddListener(OnCreate);

    void OnCreate()
    {
        string pw = passwordInput.text;

        if (string.IsNullOrWhiteSpace(pw))
        {
            if (statusText != null) statusText.text = "Şifre gerekli";
            return;
        }

        createButton.interactable = false;
        if (statusText != null) statusText.text = "Relay oluşturuluyor...";

        ((LobbyNetworkManager)NetworkManager.singleton).HostLobby(pw);
    }
}