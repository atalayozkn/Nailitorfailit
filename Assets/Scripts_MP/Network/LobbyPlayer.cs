using System.Collections;
using Mirror;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName;

    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady;

    [SyncVar(hook = nameof(OnHostChanged))]
    public bool isHost;

    PlayerListEntryUI uiRow;

    public override void OnStartClient()
    {
        // LobbyUIManager, lobby panelinin (inaktif) icinde duruyor; panel aktif edilene
        // kadar Instance null olur. Bu yuzden satiri hemen eklemeye calismak yerine
        // Instance hazir olana kadar kisa sure bekliyoruz (host satiri kaybolmasin).
        StartCoroutine(AddRowWhenReady());

        if (isOwned && GameManager.Instance != null)
            GameManager.Instance.ShowLobbyMenu();
    }

    private IEnumerator AddRowWhenReady()
    {
        float waited = 0f;
        while (LobbyUIManager.Instance == null && waited < 10f)
        {
            yield return new WaitForSeconds(0.1f);
            waited += 0.1f;
        }

        if (LobbyUIManager.Instance == null)
        {
            Debug.LogWarning("[Lobby] LobbyUIManager bulunamadi; oyuncu satiri eklenemedi.");
            yield break;
        }

        uiRow = LobbyUIManager.Instance.AddPlayerRow(this);

        // SyncVar hook'lari uiRow atanmadan once calismis olabilir; degerleri simdi uygula.
        ApplyRowData();

        // Ayrica 2 saniye boyunca periyodik olarak tekrar uygula: SyncVar gecikmeli
        // gelirse (veya hook kacarsa) isim/renk yine de dogru gorunur.
        StartCoroutine(RefreshRowBriefly());
    }

    private void ApplyRowData()
    {
        if (uiRow == null) return;

        string displayName = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
        uiRow.SetName(displayName);
        uiRow.UpdateColor(isHost, isReady);
    }

    private IEnumerator RefreshRowBriefly()
    {
        float waited = 0f;
        while (waited < 2f)
        {
            yield return new WaitForSeconds(0.25f);
            waited += 0.25f;
            ApplyRowData();
        }
    }

    public override void OnStopClient()
    {
        if (uiRow != null && LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.RemovePlayerRow(uiRow);
    }

    void OnNameChanged(string oldVal, string newVal)
    {
        if (uiRow != null) uiRow.SetName(string.IsNullOrEmpty(newVal) ? "Player" : newVal);
    }

    void OnReadyChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(isHost, newVal);
    void OnHostChanged(bool oldVal, bool newVal) => uiRow?.UpdateColor(newVal, isReady);

    /// <summary>
    /// Sunucu tarafinda ismi tekrar yayinlar. Mirror SyncVar setter'i ayni degerde
    /// dirty biti isaretlemez; bu yuzden once farkli bir deger yazip gercek degeri
    /// geri koyuyoruz. Boylece delta paketi tum istemcilere gonderilir ve isimler
    /// (host + client) her iki tarafta da kesin gorunur.
    /// </summary>
    [Server]
    public void ServerForceNameSync()
    {
        string real = playerName;
        playerName = real + "\u200b";   // zero-width space: farkli deger
        playerName = real;                // gercek deger -> delta yayinlanir
    }

    [Command]
    public void CmdSetReady(bool ready)
    {
        if (isHost) return;
        isReady = ready;
    }
}
