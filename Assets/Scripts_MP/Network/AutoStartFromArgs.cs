using System;
using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Komut satiri argumanlariyla otomatik lobi baslatma (ParrelSync / build testleri icin).
///
/// Kullanim:
///   -host               → oyun baslar baslamaz otomatik "Create Lobby" yapar
///   -client             → LAN modunda 127.0.0.1'e otomatik Join yapar.
///                         Steam modunda calismaz (asagiya bakin) — -join=CODE kullanin.
///   -join=CODE          → verilen 6 haneli lobby koduna (Steam) veya host IP'sine (LAN) Join yapar
///   +connect_lobby ID   → Steam'in davet ile baslatirken ekledigi arguman; sifre sorulur
///
/// ParrelSync Clones Manager'daki "Arguments" alanina "client" yazarsaniz, argumanin basina
/// tire eklemeyi unutmayin: "-client". (ParrelSync argumani oldugu gibi gecirir.)
///
/// Bu bilesen kendini kendisi olusturur (RuntimeInitializeOnLoadMethod), boylece
/// SteamLobbyManager'in Start() sirasina bagimli degildir.
/// </summary>
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

        // Steam daveti: oyun kapaliyken davet kabul edilirse Steam "+connect_lobby <id>"
        // argumaniyla baslatir.
        string connectLobby = GetConnectLobbyArg(args);

        if (!isHost && !isClient && string.IsNullOrEmpty(joinTarget) && string.IsNullOrEmpty(connectLobby))
            return;   // arguman yok → normal akis

        StartCoroutine(AutoJoinRoutine(isHost, isClient, joinTarget, connectLobby));
    }

    private IEnumerator AutoJoinRoutine(bool isHost, bool isClient, string joinTarget, string connectLobby)
    {
        // Sahnenin ve NetworkManager'in hazir olmasini bekle.
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
            // Oyun kapaliyken gelen davet: Steam bizi "+connect_lobby <id>" ile baslatir.
            // Lobiye katilip kodu cozeriz; sifre kullaniciya sorulur
            // (SteamLobbyManager.OnInviteCodeReady -> Join ekrani doldurulur).
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

        // -client: yalnizca LAN modunda anlamli. Steam modunda "127.0.0.1" ne
        // SteamID64 ne de 6 haneli koddur; sessizce basarisiz olmak yerine uyariyoruz.
        bool lanMode = SteamLobbyManager.Instance == null || SteamLobbyManager.Instance.UseLanDevMode;

        if (!lanMode)
        {
            Debug.LogError("[AUTO] -client Steam modunda desteklenmiyor. " +
                           "Ikinci oyuncu icin '-join=<6 haneli kod>' kullanin " +
                           "(veya LAN testi icin SteamLobbyManager.forceLanMode acin / NIF_LAN kullanin).");
            yield break;
        }

        Debug.Log("[AUTO] -client algilandi → 127.0.0.1'e baglaniliyor.");
        nm.JoinLobby("127.0.0.1", defaultPassword);
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

    /// <summary>Steam'in davet akisinda ekledigi "+connect_lobby 123456789" argumanini okur.</summary>
    private static string GetConnectLobbyArg(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i].Trim(), "+connect_lobby", StringComparison.OrdinalIgnoreCase))
                return args[i + 1].Trim();
        return null;
    }
}
