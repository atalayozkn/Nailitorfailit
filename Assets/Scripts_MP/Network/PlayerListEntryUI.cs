// ============================================================
// File: PlayerListEntryUI.cs
// Author: Murad
// Created: 30-Jun-2026
// Purpose: Utility methods for displaying player information in the lobby
// ============================================================

using TMPro;
using UnityEngine;

public class PlayerListEntryUI : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;

    static readonly Color HostColor     = Color.yellow;
    static readonly Color ReadyColor    = Color.green;
    static readonly Color NotReadyColor = Color.red;

    public void SetName(string name) => nameText.text = name;

    public void UpdateColor(bool isHost, bool isReady)
    {
        nameText.color = isHost ? HostColor : (isReady ? ReadyColor : NotReadyColor);
    }
}