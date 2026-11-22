using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
    private const string Label = "Compete";
    private const string CoopLabel = "Together";
    private const string ModeGroupName = "Buttons_ModeChoice";
    private const string BackdropName = "Buttons_Backdrop";
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

        // Together / Compete open a second step with Create and Join, so that step is a copy of the whole panel
        // (made first: the copy keeps the Create / Join wiring of the scene, and its panel references point at itself).
        GameObject modeGroup = Instantiate(panel.gameObject, panel.parent, false);
        modeGroup.name = ModeGroupName;
        modeGroup.SetActive(false);
        BuildModeGroup(modeGroup.transform, panel.gameObject);

        GameObject clone = Instantiate(create.gameObject, panel, false);
        clone.name = "Button_Versus";

        Transform back = panel.Find(BackName);
        Transform join = panel.Find(JoinName);
        if (join != null) Destroy(join.gameObject); // Join lives inside the two modes now.
        LayoutTopRows(panel, create, clone.transform, back);

        SetLabel(clone, Label);
        SetLabel(create.gameObject, CoopLabel);

        OpenModeOnClick(create.GetComponent<Button>(), false, panel.gameObject, modeGroup);
        OpenModeOnClick(clone.GetComponent<Button>(), true, panel.gameObject, modeGroup);

        Debug.Log("[VERSUS] Together / Compete mode choice added to the multiplayer menu.");
    }

    // Top step: Tutorial, Together, Compete, Exit - the same rows and spacing as the main menu.
    private static void LayoutTopRows(Transform panel, Transform together, Transform compete, Transform back)
    {
        Transform tutorial = panel.Find("Button_Tutorial");
        Transform[] rows = { tutorial, together, compete, back };
        PlaceRows(panel, rows, new[] { 203.5f, 65.5f, -72.5f, -210.5f }, new Vector2(516f, 632f), new Vector2(480f, -3.5f));
        compete.SetSiblingIndex(back != null ? back.GetSiblingIndex() : compete.GetSiblingIndex());
    }

    // Second step: Create, Join, Back - the same rows as the singleplayer sub menu.
    private static void BuildModeGroup(Transform group, GameObject topPanel)
    {
        Transform tutorial = group.Find("Button_Tutorial");
        if (tutorial != null) Destroy(tutorial.gameObject);

        Transform create = group.Find(CreateName);
        Transform join = group.Find(JoinName);
        Transform back = group.Find(BackName);
        if (create == null || join == null || back == null) return;

        SetLabel(create.gameObject, "Create");

        // The scene wiring of Back leaves the multiplayer panel; here it only returns to the first step.
        Button backButton = back.GetComponent<Button>();
        DisablePersistent(backButton);
        if (backButton != null)
        {
            GameObject g = group.gameObject;
            backButton.onClick.AddListener(() => { g.SetActive(false); topPanel.SetActive(true); });
        }

        PlaceRows(group, new[] { create, join, back }, new[] { 172.8f, -3.5f, -179.8f }, new Vector2(516f, 572f), new Vector2(480f, -3.5f));
    }

    private static void PlaceRows(Transform group, Transform[] rows, float[] ys, Vector2 backdropSize, Vector2 backdropPos)
    {
        for (int i = 0; i < rows.Length && i < ys.Length; i++)
        {
            if (rows[i] == null) continue;
            var rect = (RectTransform)rows[i];
            rect.anchoredPosition = new Vector2(backdropPos.x, ys[i]);
            rect.localScale = Vector3.one * MainMenuScale;
        }

        Transform backdrop = group.Find(BackdropName);
        if (backdrop != null)
        {
            var rect = (RectTransform)backdrop;
            rect.anchoredPosition = backdropPos;
            rect.sizeDelta = backdropSize;
        }
    }

    private static void DisablePersistent(Button button)
    {
        if (button == null) return;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
    }

    // Together / Compete: remember the mode, then show Create / Join.
    private static void OpenModeOnClick(Button button, bool versus, GameObject topPanel, GameObject modeGroup)
    {
        if (button == null) return;
        DisablePersistent(button);
        button.onClick.AddListener(() =>
        {
            SetPending(versus);
            topPanel.SetActive(false);
            modeGroup.SetActive(true);
        });
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
