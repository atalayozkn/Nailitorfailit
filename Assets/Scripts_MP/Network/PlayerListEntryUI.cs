
using TMPro;
using UnityEngine;

public class PlayerListEntryUI : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;

    static readonly Color HostColor     = Color.yellow;
    static readonly Color ReadyColor    = Color.green;
    static readonly Color NotReadyColor = Color.red;

    [SerializeField] int maxNameLength = 16;

    private Color playerColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    public void SetName(string name)
    {
        if (string.IsNullOrEmpty(name)) name = "Player";

        if (maxNameLength > 0 && name.Length > maxNameLength)
            name = name.Substring(0, maxNameLength - 1) + "\u2026";

        if (nameText != null) nameText.text = name;
    }

    public void SetPlayerColor(Color color) => playerColor = color;

    public void UpdateColor(bool isHost, bool isReady)
    {
        if (nameText == null) return;

        nameText.color = playerColor;

        if (isHost) nameText.fontStyle = FontStyles.Bold;
        else if (isReady) nameText.fontStyle = FontStyles.Underline;
        else nameText.fontStyle = FontStyles.Normal;
    }
}
