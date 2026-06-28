using System.Net;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class ServerPanelManager : MonoBehaviour
{
    [SerializeField] private GameObject            lobbyRowPrefab;
    [SerializeField] private Transform             content;
    [SerializeField] private CustomNetworkDiscovery networkDiscovery;
    [SerializeField] private Button                joinServerButton;
    [SerializeField] private PasswordPromptPanel   passwordPrompt;

    private CustomServerResponse                        selectedResponse;
    private readonly Dictionary<IPEndPoint, GameObject> discoveredServers = new();

    void OnEnable()
    {
        discoveredServers.Clear();
        networkDiscovery.OnServerFound.AddListener(OnServerFound);
        networkDiscovery.StartDiscovery();
        joinServerButton.interactable = false;
    }

    void OnDisable()
    {
        networkDiscovery.OnServerFound.RemoveListener(OnServerFound);
        networkDiscovery.StopDiscovery();

        foreach (Transform child in content)
            Destroy(child.gameObject);

        discoveredServers.Clear();
    }

    void OnServerFound(CustomServerResponse response)
    {
        if (discoveredServers.ContainsKey(response.EndPoint)) return;

        GameObject row    = Instantiate(lobbyRowPrefab, content);
        LobbyRow   lobbyRow = row.GetComponent<LobbyRow>();
        lobbyRow.Setup(response, this);

        discoveredServers[response.EndPoint] = row;
    }

    public void OnRowSelected(CustomServerResponse response)
    {
        selectedResponse              = response;
        joinServerButton.interactable = true;
    }

    public void OnJoinClicked()
    {
        passwordPrompt.Open(selectedResponse, this);
    }

    /// <summary>
    /// "Refresh" button: drop the current list + selection and re-scan the LAN.
    /// Discovery responses trickle back in via OnServerFound, repopulating the rows.
    /// </summary>
    public void Refresh()
    {
        networkDiscovery.StopDiscovery();

        foreach (Transform child in content)
            Destroy(child.gameObject);

        discoveredServers.Clear();
        selectedResponse              = null;
        joinServerButton.interactable = false;

        networkDiscovery.StartDiscovery();
    }

    public void ConfirmJoin(CustomServerResponse response, string enteredPassword)
    {
        string enteredHash = CustomNetworkDiscovery.HashPassword(enteredPassword);

        if (enteredHash != response.passwordHash)
        {
            passwordPrompt.ShowError("Wrong password.");
            return;
        }

        networkDiscovery.StopDiscovery();
        NetworkManager.singleton.StartClient(response.uri);
        passwordPrompt.Close();
    }
}