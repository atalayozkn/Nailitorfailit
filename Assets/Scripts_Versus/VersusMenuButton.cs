using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VersusMenuButton : MonoBehaviour
{
    private const string MenuScene = "MenuScene";
    private const string PanelName = "Buttons_AfterMultiplayer";
    private const string CreateName = "Button_Create";
    private const string JoinName = "Button_Join";
    private const string BackName = "Button_BackToMenu";
    private const string VersusScenePath = "Assets/Scenes/VersusMultiplayer_Test.unity";
    private const string Label = "Confrontation";
    private const string CoopLabel = "Together";
    private const int VersusPlayers = 2;
    // Five rows (tutorial, create, join, versus + the quit icon) do not fit at the old 200 spacing: the quit icon fell off
    // the screen and sat on the social icons. The four big rows are squeezed together (buttons scaled to match).
    private const float RowSpacing = 200f;
    private const float RowStep = 152f;
    private const float MainMenuScale = 0.105f; // Button_SinglePlayer / Multiplayer scale in MenuScene

    public static bool VersusLobbyPending { get; private set; }

    private static VersusMenuButton driver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

    private static void TryInstall(Scene scene)
    {
        if (scene.name != MenuScene) return;

        EnsureDriver();

        Transform panel = FindInScene(scene, PanelName);
        if (panel == null)
        {
            Debug.LogWarning($"[VERSUS] '{PanelName}' not found in {MenuScene}; Confrontation button not added.");
            return;
        }

        if (panel.Find("Button_Versus") != null) return;

        Transform create = panel.Find(CreateName);
        if (create == null)
        {
            Debug.LogWarning($"[VERSUS] '{CreateName}' not found under {PanelName}; Confrontation button not added.");
            return;
        }

        GameObject clone = Instantiate(create.gameObject, panel, false);
        clone.name = "Button_Versus";

        Transform back = panel.Find(BackName);
        LayoutRows(panel, create, clone.transform, back);

        SetLabel(clone, Label);
        SetLabel(create.gameObject, CoopLabel);

        Button versusButton = clone.GetComponent<Button>();
        if (versusButton != null) versusButton.onClick.AddListener(() => SetPending(true));

        AddClear(create);
        AddClear(panel.Find(JoinName));

        Debug.Log("[VERSUS] Confrontation button added to the multiplayer menu.");
    }

    private static void LayoutRows(Transform panel, Transform create, Transform versus, Transform back)
    {
        Transform tutorial = panel.Find("Button_Tutorial");
        Transform join = panel.Find(JoinName);
        if (tutorial == null || create == null || join == null) return;

        // The quit button is the fifth row of the same stack.
        // Order: Tutorial, Together, Confrontation, Join, Exit.
        Transform[] rows = back != null ? new[] { tutorial, create, versus, join, back } : new[] { tutorial, create, versus, join };
        float top = ((RectTransform)tutorial).anchoredPosition.y;

        for (int i = 0; i < rows.Length; i++)
        {
            var rect = (RectTransform)rows[i];
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top - i * RowStep);
            rect.localScale = Vector3.one * MainMenuScale; // same size as the main menu buttons
        }

        versus.SetSiblingIndex(back != null ? back.GetSiblingIndex() : versus.GetSiblingIndex());
    }

    private static void AddClear(Transform buttonTransform)
    {
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        if (button != null) button.onClick.AddListener(() => SetPending(false));
    }

    private static void SetPending(bool pending)
    {
        VersusLobbyPending = pending;
        LobbyNetworkManager.MaxPlayersOverride = pending ? VersusPlayers : 0;
        Debug.Log($"[VERSUS] versus lobby pending -> {pending}");
    }

    private static void SetLabel(GameObject button, string label)
    {
        bool found = false;

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = label;
            text.enableAutoSizing = true;
            text.fontSizeMin = 60f;
            text.fontSizeMax = text.fontSize;
            found = true;
        }

        foreach (Text text in button.GetComponentsInChildren<Text>(true))
        {
            text.text = label;
            found = true;
        }

        if (!found) Debug.LogWarning("[VERSUS] Versus button has no text component; it will look like Create.");
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
        }

        return null;
    }

    private static void EnsureDriver()
    {
        if (driver != null) return;

        GameObject go = new GameObject("VersusMenuButton");
        DontDestroyOnLoad(go);
        driver = go.AddComponent<VersusMenuButton>();
    }

    private void Update()
    {
        if (!NetworkServer.active) return;

        MpLevelCatalog catalog = MpLevelCatalog.Instance;
        if (catalog == null || MpLevelNet.Instance == null) return;

        // Co-op lobby: the versus level must not stay selected (left over from a versus lobby).
        if (!VersusLobbyPending)
        {
            if (!catalog.IsSelectable(catalog.SelectedIndex)) MpLevelNet.Instance.HostSelect(0);
            return;
        }

        int index = VersusIndex(catalog);
        if (index < 0 || catalog.SelectedIndex == index) return;

        MpLevelNet.Instance.HostSelect(index);
    }

    private static int VersusIndex(MpLevelCatalog catalog)
    {
        for (int i = 0; i < catalog.Count; i++)
        {
            if (catalog.Get(i).scenePath == VersusScenePath) return i;
        }

        return -1;
    }
}
