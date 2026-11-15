using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VersusMenuButton : MonoBehaviour
{
    private const string MenuScene = "MenuScene_MP";
    private const string PanelName = "Buttons_AfterMultiplayer";
    private const string CreateName = "Button_Create";
    private const string JoinName = "Button_Join";
    private const string BackName = "Button_BackToMenu";
    private const string VersusScenePath = "Assets/Scenes/VersusMultiplayer_Test.unity";
    private const string Label = "Versus Mode";
    private const int VersusPlayers = 2;
    private const float RowSpacing = 200f;

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
            Debug.LogWarning($"[VERSUS] '{PanelName}' not found in {MenuScene}; Versus Mode button not added.");
            return;
        }

        if (panel.Find("Button_Versus") != null) return;

        Transform create = panel.Find(CreateName);
        if (create == null)
        {
            Debug.LogWarning($"[VERSUS] '{CreateName}' not found under {PanelName}; Versus Mode button not added.");
            return;
        }

        GameObject clone = Instantiate(create.gameObject, panel, false);
        clone.name = "Button_Versus";

        Transform back = panel.Find(BackName);
        RectTransform cloneRect = (RectTransform)clone.transform;
        if (back != null)
        {
            RectTransform backRect = (RectTransform)back;
            cloneRect.anchoredPosition = backRect.anchoredPosition;
            backRect.anchoredPosition -= new Vector2(0f, RowSpacing);
            clone.transform.SetSiblingIndex(back.GetSiblingIndex());
        }
        else
        {
            cloneRect.anchoredPosition -= new Vector2(0f, RowSpacing * 2f);
        }

        SetLabel(clone);

        Button versusButton = clone.GetComponent<Button>();
        if (versusButton != null) versusButton.onClick.AddListener(() => SetPending(true));

        AddClear(create);
        AddClear(panel.Find(JoinName));

        Debug.Log("[VERSUS] Versus Mode button added to the multiplayer menu.");
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

    private static void SetLabel(GameObject button)
    {
        bool found = false;

        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = Label;
            found = true;
        }

        foreach (Text text in button.GetComponentsInChildren<Text>(true))
        {
            text.text = Label;
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
        if (!VersusLobbyPending || !NetworkServer.active) return;

        MpLevelCatalog catalog = MpLevelCatalog.Instance;
        if (catalog == null || MpLevelNet.Instance == null) return;

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
