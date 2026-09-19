
using TMPro;
using UnityEngine;

public class PlayerListEntryUI : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;

    static readonly Color HostColor     = Color.yellow;
    static readonly Color ReadyColor    = Color.green;
    static readonly Color NotReadyColor = Color.red;

    [SerializeField] int maxNameLength = 16;

    public void SetName(string name)
    {
        if (string.IsNullOrEmpty(name)) name = "Player";

        // Cok uzun isimler satirdan tasmasin: kirp ve uc nokta koy.
        if (maxNameLength > 0 && name.Length > maxNameLength)
            name = name.Substring(0, maxNameLength - 1) + "\u2026";

        if (nameText != null) nameText.text = name;
    }

    public void UpdateColor(bool isHost, bool isReady)
    {
        nameText.color = isHost ? HostColor : (isReady ? ReadyColor : NotReadyColor);
    }
}
