using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// End-of-match buttons next to the existing "Main menu" one: Play again, Next level, Back to the lobby.
// They are copies of the main menu button (same look). Only the host can use the first three, because they move
// everybody; the others see them greyed out. Main menu stays for everyone (leaves the match).
public static class MpEndButtons_MP
{
    private const string Marker = "MpEndButton";

    public static void Build(GameObject endPanel)
    {
        if (endPanel == null) return;

        Button mainMenu = FindMainMenuButton(endPanel);
        if (mainMenu == null)
        {
            Debug.LogWarning("[MP-END] Button_MainMenu not found: end-of-match buttons not added.");
            return;
        }

        if (mainMenu.transform.parent.Find(Marker + "_Replay") != null) return;

        var manager = NetworkManager.singleton as LobbyNetworkManager;
        bool host = NetworkServer.active;

        // 2 x 2 grid where the big main menu button used to be:
        //     [Play again]  [Next level]
        //     [Lobby]       [Main menu]
        // The button is drawn through its own local scale (its rect is thousands of units wide), so sizes and steps
        // are measured in the parent's coordinates.
        var rect = (RectTransform)mainMenu.transform;
        rect.localScale *= GridScale;

        float width = rect.rect.width * Mathf.Abs(rect.localScale.x);
        float height = rect.rect.height * Mathf.Abs(rect.localScale.y);
        float gap = width * 0.05f;

        Vector2 center = rect.anchoredPosition;
        Vector2 right = new Vector2(width * 0.5f + gap * 0.5f, 0f);
        Vector2 up = new Vector2(0f, height * 0.5f + gap * 0.5f);

        rect.anchoredPosition = center + right - up;
        Add(mainMenu, "Replay", "Play again", center - right + up, host && manager != null, () => manager.HostReplayLevel());
        Add(mainMenu, "Next", "Next level", center + right + up, host && manager != null && manager.HasNextLevel, () => manager.HostNextLevel());
        Add(mainMenu, "Lobby", "Back to lobby", center - right - up, host && manager != null, () => manager.HostReturnToLobby());
    }

    private const float GridScale = 0.55f;

    private static Button FindMainMenuButton(GameObject endPanel)
    {
        foreach (Button button in endPanel.GetComponentsInChildren<Button>(true))
            if (button.name == "Button_MainMenu") return button;

        return null;
    }

    private static void Add(Button template, string id, string label, Vector2 position, bool usable, System.Action action)
    {
        Button button = Object.Instantiate(template, template.transform.parent);
        button.name = Marker + "_" + id;
        ((RectTransform)button.transform).anchoredPosition = position;

        // The copy would otherwise keep the template's persistent "go to main menu" call.
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() =>
        {
            Debug.Log($"[MP-END] '{label}' pressed.");
            action();
        });

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            text.text = usable || NetworkServer.active ? label : label + " (host)";

        button.interactable = usable;
    }
}
