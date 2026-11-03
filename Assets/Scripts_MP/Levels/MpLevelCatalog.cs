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

    public void Select(int index)
    {
        if (Count == 0) return;
        selectedIndex = Mathf.Clamp(index, 0, Count - 1);
    }

    public void SelectNext() => Select(SelectedIndex + 1);

    public void SelectPrevious() => Select(SelectedIndex - 1);

    public string SelectedLabel()
    {
        if (Count == 0) return "no levels in catalog";
        return $"{Selected.displayName}  ({SelectedIndex + 1}/{Count})";
    }
}
