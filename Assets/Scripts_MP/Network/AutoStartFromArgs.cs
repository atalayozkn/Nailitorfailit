using System;
using System.Collections;
using Mirror;
using UnityEngine;

public class AutoStartFromArgs : MonoBehaviour
{
    [SerializeField] private float startupDelay = 1.5f;
    [SerializeField] private string defaultPassword = "1111";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<AutoStartFromArgs>() != null) return;
        new GameObject("AutoStartFromArgs").AddComponent<AutoStartFromArgs>();
    }

    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();

        bool isHost = HasArg(args, "-host");
        bool isClient = HasArg(args, "-client") || HasArg(args, "client");
        string joinTarget = GetArgValue(args, "-join");

        string connectLobby = GetConnectLobbyArg(args);

        if (!isHost && !isClient && string.IsNullOrEmpty(joinTarget) && string.IsNullOrEmpty(connectLobby))
            return;

        StartCoroutine(AutoJoinRoutine(isHost, isClient, joinTarget, connectLobby));
    }

    private IEnumerator AutoJoinRoutine(bool isHost, bool isClient, string joinTarget, string connectLobby)
    {

        yield return new WaitForSeconds(startupDelay);

        LobbyNetworkManager nm = null;
        float waited = 0f;
        while (nm == null && waited < 10f)
        {
            nm = NetworkManager.singleton as LobbyNetworkManager;
            if (nm == null) { yield return new WaitForSeconds(0.5f); waited += 0.5f; }
        }

        if (nm == null)
        {
            Debug.LogError("[AUTO] LobbyNetworkManager bulunamadi; otomatik baslatma iptal.");
            yield break;
        }

        if (isHost)
        {
            Debug.Log("[AUTO] -host algilandi → otomatik Create Lobby.");
            nm.HostLobby(defaultPassword);
            yield break;
        }

        if (!string.IsNullOrEmpty(connectLobby))
        {

            Debug.Log($"[AUTO] +connect_lobby {connectLobby} algilandi → lobiye katiliniyor.");

            if (SteamLobbyManager.Instance == null)
            {
                Debug.LogError("[AUTO] SteamLobbyManager bulunamadi; davet islenemedi.");
                yield break;
            }

            if (!ulong.TryParse(connectLobby, out ulong lobbyId))
            {
                Debug.LogError("[AUTO] +connect_lobby degeri sayisal degil: " + connectLobby);
                yield break;
            }

            SteamLobbyManager.Instance.JoinLobbyFromInvite(new Steamworks.CSteamID(lobbyId));
            yield break;
        }

        if (!string.IsNullOrEmpty(joinTarget))
        {
            Debug.Log($"[AUTO] otomatik Join → '{joinTarget}'");
            nm.JoinLobby(joinTarget, defaultPassword);
            yield break;
        }

        Debug.LogError("[AUTO] -client Steam modunda desteklenmiyor. " +
                       "Ikinci oyuncu icin '-join=<6 haneli kod>' kullanin.");
    }

    private static bool HasArg(string[] args, string name)
    {
        foreach (string a in args)
            if (string.Equals(a.Trim(), name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string GetArgValue(string[] args, string prefix)
    {
        foreach (string a in args)
            if (a.StartsWith(prefix + "=", StringComparison.OrdinalIgnoreCase))
                return a.Substring(prefix.Length + 1);
        return null;
    }

    private static string GetConnectLobbyArg(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i].Trim(), "+connect_lobby", StringComparison.OrdinalIgnoreCase))
                return args[i + 1].Trim();
        return null;
    }
}
