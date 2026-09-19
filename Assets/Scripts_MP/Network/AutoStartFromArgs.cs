using System;
using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Komut satiri argumanlariyla otomatik lobi baslatma (ParrelSync / build testleri icin).
///
/// Kullanim:
///   -host     → oyun baslar baslamaz otomatik "Create Lobby" yapar
///   -client   → oyun baslar baslamaz 127.0.0.1 adresine otomatik Join yapar
///   -join=CODE → verilen 6 haneli lobby koduna otomatik Join yapar
///
/// ParrelSync Clones Manager'daki "Arguments" alanina "client" yazarsaniz, argumanin basina
/// tire eklemeyi unutmayin: "-client". (ParrelSync argumani oldugu gibi gecirir.)
/// </summary>
public class AutoStartFromArgs : MonoBehaviour
{
    [SerializeField] private float startupDelay = 1.5f;
    [SerializeField] private string defaultPassword = "1111";

    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();

        bool isHost = HasArg(args, "-host");
        bool isClient = HasArg(args, "-client") || HasArg(args, "client");
        string joinTarget = GetArgValue(args, "-join");

        if (!isHost && !isClient && string.IsNullOrEmpty(joinTarget))
            return;   // arguman yok → normal akis

        StartCoroutine(AutoJoinRoutine(isHost, isClient, joinTarget));
    }

    private IEnumerator AutoJoinRoutine(bool isHost, bool isClient, string joinTarget)
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
        }
        else
        {
            string target = !string.IsNullOrEmpty(joinTarget)
                ? joinTarget
                : (isClient ? "127.0.0.1" : null);

            if (string.IsNullOrEmpty(target)) yield break;

            Debug.Log($"[AUTO] otomatik Join → '{target}'");
            nm.JoinLobby(target, defaultPassword);
        }
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
}
