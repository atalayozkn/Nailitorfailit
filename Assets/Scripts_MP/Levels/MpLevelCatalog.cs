using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct MpLevelEntry
{
    public string displayName;
    public string scenePath;
}

/// <summary>
/// The multiplayer level list (M3 of the modular MP system).
///
/// The old code had a single const GameScenePath, so every match went to the same
/// level. Here the levels live in a list the host picks from, and everything else
/// (lobby UI, ServerChangeScene) asks this catalog what to load.
///
/// Self-spawning DDOL singleton: no scene wiring, works from any menu scene.
/// </summary>
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

    /// <summary>Seeds the catalog with the levels in the project's Scenes folder.</summary>
    private void EnsureDefaults()
    {
        if (levels != null && levels.Count > 0) return;

        levels = new List<MpLevelEntry>
        {
            Entry("Level 01 - Arda",     "Assets/Scenes/Level_01_Arda.unity"),
            Entry("Level 01 - Ardav2",   "Assets/Scenes/Level_01_Ardav2.unity"),
            Entry("Level 01 - Tarik",    "Assets/Scenes/Level_01_TARIK.unity"),
            Entry("Level 02 - Tarik",    "Assets/Scenes/Level_02_TARIK.unity"),
            Entry("Level 03 - Tarik",    "Assets/Scenes/Level_03_TARIK.unity"),
            Entry("Level 05 - Tarik",    "Assets/Scenes/Level_05_TARIK.unity"),
            Entry("Level 06 - Tarik",    "Assets/Scenes/Level_06_TARIK.unity"),
        };

        Debug.Log($"[MP-LEVELS] catalog seeded with {levels.Count} level(s) from the Scenes folder.");
    }

    private static MpLevelEntry Entry(string name, string path) =>
        new MpLevelEntry { displayName = name, scenePath = path };

    public MpLevelEntry Get(int index)
    {
        if (Count == 0) return default;
        return levels[Mathf.Clamp(index, 0, Count - 1)];
    }

    public void Select(int index)
    {
        if (Count == 0) return;
        selectedIndex = Mathf.Clamp(index, 0, Count - 1);
    }

    public void SelectNext() => Select(SelectedIndex + 1);

    public void SelectPrevious() => Select(SelectedIndex - 1);

    /// <summary>Display text for the lobby label, e.g. "Level 2 - Tarik (2/7)".</summary>
    public string SelectedLabel()
    {
        if (Count == 0) return "no levels in catalog";
        return $"{Selected.displayName}  ({SelectedIndex + 1}/{Count})";
    }
}
