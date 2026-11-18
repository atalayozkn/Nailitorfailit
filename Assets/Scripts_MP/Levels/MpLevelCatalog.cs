using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public struct MpLevelEntry
{
    public string displayName;
    public string scenePath;
}

public class MpLevelCatalog : MonoBehaviour
{
    public static MpLevelCatalog Instance { get; private set; }

    [Tooltip("Levels offered in the lobby. Seeded with the Scenes-folder levels on first run.")]
    [SerializeField] private List<MpLevelEntry> levels = new List<MpLevelEntry>();

    private int selectedIndex;

    public int Count => levels != null ? levels.Count : 0;
    public int SelectedIndex => Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, Count - 1));
    public MpLevelEntry Selected => Get(SelectedIndex);
    public string SelectedScenePath => Count > 0 ? Selected.scenePath : string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpLevelCatalog>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpLevelCatalog").AddComponent<MpLevelCatalog>();
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
        EnsureDefaults();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsureDefaults()
    {
        if (levels != null && levels.Count > 0) return;

        levels = new List<MpLevelEntry>
        {
            Entry("Level 01",           "Assets/Scenes/Level_01.unity"),
            Entry("Level 02",           "Assets/Scenes/Level_02.unity"),
            Entry("Level 03",           "Assets/Scenes/Level_03.unity"),
            Entry("Level 04",           "Assets/Scenes/Level_04.unity"),
            Entry("Level 05",           "Assets/Scenes/Level_05.unity"),
            Entry("Versus Test (2P)",   "Assets/Scenes/VersusMultiplayer_Test.unity"),
        };

        Debug.Log($"[MP-LEVELS] catalog seeded with {levels.Count} level(s) from the Scenes folder.");

        ValidateAgainstBuildSettings();
    }

    private void ValidateAgainstBuildSettings()
    {
        if (levels == null) return;

        foreach (MpLevelEntry entry in levels)
        {
            if (string.IsNullOrEmpty(entry.scenePath)) continue;
            if (IsInBuildSettings(entry.scenePath)) continue;

            Debug.LogWarning($"[MP-LEVELS] '{entry.scenePath}' build profilinde yok; build'de acilamaz. " +
                             "Tools > MP > 8 ile ekleyebilirsin.");
        }
    }

    private static bool IsInBuildSettings(string scenePath)
    {
        int count = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < count; i++)
        {
            if (SceneUtility.GetScenePathByBuildIndex(i) == scenePath) return true;
        }

        return false;
    }

    private static MpLevelEntry Entry(string name, string path) =>
        new MpLevelEntry { displayName = name, scenePath = path };

    public MpLevelEntry Get(int index)
    {
        if (Count == 0) return default;
        return levels[Mathf.Clamp(index, 0, Count - 1)];
    }

    // The versus test scene belongs to Versus Mode only: a normal lobby must not be able to pick it, and stepping
    // through the list skips it.
    public static bool IsVersusScene(string scenePath) =>
        !string.IsNullOrEmpty(scenePath) &&
        scenePath.IndexOf("Versus", System.StringComparison.OrdinalIgnoreCase) >= 0;

    public bool IsSelectable(int index)
    {
        if (Count == 0) return false;

        // Co-op lobby: Level 1-5 only. Versus lobby: the versus level only.
        MpLevelEntry entry = levels[Mathf.Clamp(index, 0, Count - 1)];
        return IsVersusScene(entry.scenePath) == VersusMenuButton.VersusLobbyPending;
    }

    public void Select(int index)
    {
        if (Count == 0) return;

        int wanted = Mathf.Clamp(index, 0, Count - 1);
        if (!IsSelectable(wanted))
        {
            Debug.Log($"[MP-LEVELS] '{levels[wanted].displayName}' is Versus Mode only; selection ignored.");
            return;
        }

        selectedIndex = wanted;
    }

    public void SelectNext() => SelectStep(+1);

    public void SelectPrevious() => SelectStep(-1);

    private void SelectStep(int step)
    {
        if (Count == 0) return;

        for (int i = SelectedIndex + step; i >= 0 && i < Count; i += step)
        {
            if (!IsSelectable(i)) continue;

            selectedIndex = i;
            return;
        }
    }

    public string SelectedLabel()
    {
        if (Count == 0) return "no levels in catalog";
        // Count only the levels this lobby can pick (co-op: Level 1-5, versus: the versus level).
        int total = 0, position = 0;
        for (int i = 0; i < Count; i++)
        {
            if (!IsSelectable(i)) continue;

            total++;
            if (i <= SelectedIndex) position = total;
        }

        return $"{Selected.displayName}  ({Mathf.Max(1, position)}/{Mathf.Max(1, total)})";
    }
}
