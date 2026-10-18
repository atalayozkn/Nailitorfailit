using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level picker inside the LOBBY panel (M3, part 3).
///
/// Host-only arrows (◀ / ▶) plus a label every client can see. The UI is created at
/// runtime inside the existing "Panel_Lobby" object, so no scene surgery is needed and
/// the module works in whichever scene hosts the lobby panels.
///
/// Reads/writes MpLevelCatalog (local list) and MpLevelNet (replication) only - it has
/// no idea how the match is started, which keeps it modular.
/// </summary>
public class MpLevelSelectUI : MonoBehaviour
{
    public static MpLevelSelectUI Instance { get; private set; }

    [Tooltip("Panel the picker is injected into (found by name when empty).")]
    [SerializeField] private string lobbyPanelName = "Panel_Lobby";

    [Tooltip("Vertical offset of the picker row inside the panel.")]
    [SerializeField] private float rowOffsetY = 150f;

    private TMP_Text label;
    private Button prevButton;
    private Button nextButton;
    private bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpLevelSelectUI>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpLevelSelectUI").AddComponent<MpLevelSelectUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        MpLevelNet.LevelChanged += OnLevelChanged;
    }

    private void OnDestroy()
    {
        MpLevelNet.LevelChanged -= OnLevelChanged;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            if (built) Destroy(gameObject);
            return;
        }

        if (label == null) built = false;
        if (!built) TryBuild();
        if (built) Refresh();
    }

    // ---------------------------------------------------------------- build

    private void TryBuild()
    {
        Transform panel = FindPanel();
        if (panel == null) return;

        var root = new GameObject("MpLevelPicker", typeof(RectTransform));
        root.transform.SetParent(panel, false);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, rowOffsetY);
        rect.sizeDelta = new Vector2(560f, 90f);

        prevButton = CreateButton(rect, "<<", new Vector2(-230f, 0f), () => MpLevelNet.Instance?.HostPrevious());
        nextButton = CreateButton(rect, ">>", new Vector2(230f, 0f), () => MpLevelNet.Instance?.HostNext());
        label = CreateLabel(rect);

        built = true;
        Refresh();

        Debug.Log($"[MP-LEVELS] level picker injected into '{panel.name}' " +
                  $"(host controls: {(NetworkServer.active ? "yes" : "no - client view")}).");
    }

    private Transform FindPanel()
    {
        // search inactive too: the lobby panel is disabled until the lobby opens
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform found = FindChildByName(canvas.transform, lobbyPanelName);
            if (found != null) return found;
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    private TMP_Text CreateLabel(RectTransform parent)
    {
        var go = new GameObject("LevelLabel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 34f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(1f, 0.92f, 0f, 1f);   // the game's yellow

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 60f);
        rect.anchoredPosition = Vector2.zero;

        return text;
    }

    private Button CreateButton(RectTransform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Button_{text}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(90f, 60f);
        rect.anchoredPosition = position;

        Image background = go.AddComponent<Image>();
        background.color = new Color(0.2f, 0.2f, 0.24f, 0.9f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(onClick);

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(rect, false);
        TextMeshProUGUI tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 30f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = Color.white;

        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    // ---------------------------------------------------------------- refresh

    private void Refresh()
    {
        if (label == null) return;

        MpLevelCatalog catalog = MpLevelCatalog.Instance;
        label.text = catalog != null ? catalog.SelectedLabel() : "no level catalog";

        // host-only controls; clients watch the label
        bool isHost = NetworkServer.active;
        if (prevButton != null) prevButton.gameObject.SetActive(isHost);
        if (nextButton != null) nextButton.gameObject.SetActive(isHost);
    }

    private void OnLevelChanged(int index)
    {
        Refresh();
    }
}
