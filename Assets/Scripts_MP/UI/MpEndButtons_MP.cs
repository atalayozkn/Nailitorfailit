using System.Collections.Generic;
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

        // anchoredPosition is an offset from the anchors, so a stretched template (anchorMin != anchorMax) puts every
        // copy of the grid on top of the other one. Pin the template to the centre first: a position then means
        // "this far from the middle", which is what the grid below assumes.
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

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

        HideAuthoredMainMenu(endPanel);
    }

    // The singleplayer end screen: after the score tally the sign comes back with its buttons stacked downwards from
    // the Main menu button's spot (ScoreTallyUI calls this through ScoreTallyUI.NetworkedButtons). Victory: Next level,
    // Play again, Back to lobby, Main menu. Defeat: Play again, Back to lobby, Main menu. The first three are host only.
    public static void BuildStack(IReadOnlyList<GameObject> screens, bool victory)
    {
        var manager = NetworkManager.singleton as LobbyNetworkManager;
        bool host = NetworkServer.active && manager != null;

        foreach (GameObject screen in screens)
        {
            if (screen == null) continue;

            Button mainMenu = FindMainMenuButton(screen);
            if (mainMenu == null || mainMenu.transform.parent.Find(Marker + "_Replay") != null) continue;

            var menuRect = (RectTransform)mainMenu.transform;
            Vector2 slot = menuRect.anchoredPosition;
            float step = menuRect.rect.height * menuRect.localScale.y + 12f;

            var stack = new List<Button>();
            if (victory) stack.Add(Clone(mainMenu, "Next", "Next level", host && manager.HasNextLevel, () => manager.HostNextLevel()));
            stack.Add(Clone(mainMenu, "Replay", "Play again", host, () => manager.HostReplayLevel()));
            stack.Add(Clone(mainMenu, "Lobby", "Back to lobby", host, () => manager.HostReturnToLobby()));
            stack.Add(mainMenu);

            for (int i = 0; i < stack.Count; i++)
            {
                ((RectTransform)stack[i].transform).anchoredPosition = slot - new Vector2(0f, step * i);
                ScoreTallyUI.CenterLabel(stack[i]);
            }

            if (screen.transform.parent != null) HideAuthoredMainMenu(screen.transform.parent.gameObject);
        }
    }

    private static Button Clone(Button template, string id, string label, bool usable, System.Action action)
    {
        Button button = Object.Instantiate(template, template.transform.parent);
        button.name = Marker + "_" + id;
        button.onClick = new Button.ButtonClickedEvent(); // not the template's "go to main menu" call
        button.onClick.AddListener(() =>
        {
            Debug.Log($"[MP-END] '{label}' pressed.");
            action();
        });

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            text.text = usable || NetworkServer.active ? label : label + " (host)";

        button.interactable = usable;
        return button;
    }

    // The level's own (singleplayer) end panel carries its own big "Main menu" button at the very centre, which would be
    // drawn over the four buttons above whenever that panel shows. Keep it off.
    private static void HideAuthoredMainMenu(GameObject endPanel)
    {
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button == null || button.name != "Button_MainMenu") continue;
            if (button.transform.IsChildOf(endPanel.transform)) continue;
            if (!IsUnderEndPanel(button.transform)) continue;

            button.gameObject.SetActive(false);

            Transform holder = button.transform.parent != null ? button.transform.parent : button.transform;
            if (holder.GetComponent<MpKeepChildOff_MP>() == null) holder.gameObject.AddComponent<MpKeepChildOff_MP>().Watch(button.gameObject);
        }
    }

    private static bool IsUnderEndPanel(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name.IndexOf("GameEnd", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return false;
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

        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;

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

// Keeps one child object switched off, whatever the singleplayer code does to its panel.
public class MpKeepChildOff_MP : MonoBehaviour
{
    private GameObject target;

    public void Watch(GameObject child) => target = child;

    private void LateUpdate()
    {
        if (target != null && target.activeSelf) target.SetActive(false);
    }
}
