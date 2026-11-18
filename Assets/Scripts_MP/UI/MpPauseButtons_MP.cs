using System.Reflection;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pause menu in multiplayer: Continue, Settings, Back to lobby, Main menu - one under the other.
// The pause panel ships with Continue / Settings / Main menu where Settings and Main menu sit at the SAME spot (the
// Main menu button covers Settings), so the settings could not be reached. This lays the four buttons out in a column
// and adds "Back to lobby": it brings every player back to the lobby panel, so only the host can use it (the others
// see it greyed out). Main menu stays for everyone (leaves the match).
public static class MpPauseButtons_MP
{
    private const string Marker = "MpPauseButton_Lobby";

    private static readonly FieldInfo PanelField =
        typeof(PauseMenu).GetField("pauseMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void EnsureBuilt(PauseMenu menu)
    {
        if (menu == null || PanelField == null) return;

        var panel = PanelField.GetValue(menu) as GameObject;
        if (panel == null) return;

        Transform buttons = panel.transform.Find("Buttons");
        if (buttons == null || buttons.Find(Marker) != null) return;

        Button cont = Find(buttons, "Button_Continue");
        Button settings = Find(buttons, "Button_Settings");
        Button main = Find(buttons, "Button_MainMenu");
        if (cont == null || settings == null || main == null) return;

        var contRect = (RectTransform)cont.transform;
        var settingsRect = (RectTransform)settings.transform;
        var mainRect = (RectTransform)main.transform;

        // Column: Continue, Settings, Back to lobby, Main menu, evenly spaced by the Continue -> Settings step.
        Vector2 top = contRect.anchoredPosition;
        Vector2 step = settingsRect.anchoredPosition - contRect.anchoredPosition;
        if (step.sqrMagnitude < 1f) step = new Vector2(0f, -78f);

        settingsRect.anchoredPosition = top + step;

        Button lobby = Object.Instantiate(main, buttons);
        lobby.name = Marker;
        var lobbyRect = (RectTransform)lobby.transform;
        lobbyRect.anchoredPosition = top + step * 2f;
        mainRect.anchoredPosition = top + step * 3f;
        lobbyRect.SetSiblingIndex(mainRect.GetSiblingIndex());

        var manager = NetworkManager.singleton as LobbyNetworkManager;
        bool host = NetworkServer.active && manager != null;

        // The copy would otherwise keep the template's "return to main menu" call.
        lobby.onClick = new Button.ButtonClickedEvent();
        lobby.onClick.AddListener(() =>
        {
            Debug.Log("[MP-PAUSE] 'Back to lobby' pressed.");
            menu.Resume(); // leave the pause state first (time scale, cursor) before the scene changes
            if (NetworkServer.active && manager != null) manager.HostReturnToLobby();
        });

        foreach (TMP_Text text in lobby.GetComponentsInChildren<TMP_Text>(true))
            text.text = host ? "Back to lobby" : "Back to lobby (host)";

        lobby.interactable = host;

        Debug.Log("[MP-PAUSE] pause menu laid out: Continue / Settings / Back to lobby / Main menu.");
    }

    private static Button Find(Transform root, string name)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;

        return null;
    }
}
