using UnityEngine;

public class MpBootstrap : MonoBehaviour
{
    public static MpBootstrap Instance { get; private set; }

    [Tooltip("The whole MP stack (LobbyNetworkManager + transport + Steam + chat + wallet). " +
             "Created by 'Tools → MP → Extract MP Stack Prefab'.")]
    [SerializeField] private GameObject stackPrefab;

    [Tooltip("The MP menu prefab (MpMenuUI canvas). " +
             "Created by 'Tools → MP → Extract MP Menu Prefab'. Empty = build the menu in code.")]
    [SerializeField] private GameObject menuPrefab;

    [Tooltip("Log what Open()/Close() are doing.")]
    [SerializeField] private bool logActions = true;

    public static bool IsActive => Instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpBootstrap>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpBootstrap").AddComponent<MpBootstrap>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {

            if (Instance.stackPrefab == null && stackPrefab != null)
                Instance.stackPrefab = stackPrefab;

            if (Instance.menuPrefab == null && menuPrefab != null)
            {
                Instance.menuPrefab = menuPrefab;
                if (logActions) Debug.Log($"[MP] menu prefab adopted from the scene bootstrap ('{menuPrefab.name}').");
            }

            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Open()
    {
        MpBootstrap bootstrap = Instance != null ? Instance : Ensure();
        bootstrap?.OpenInstance();
    }

    public void OpenFromButton() => OpenInstance();

    public static void Close()
    {
        MpMenuUI.Instance?.HideAll();
    }

    public static MpBootstrap Ensure()
    {
        if (Instance != null) return Instance;

        MpBootstrap existing = FindAnyObjectByType<MpBootstrap>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        return new GameObject("MpBootstrap").AddComponent<MpBootstrap>();
    }

    private void OpenInstance()
    {
        bool stackReady = EnsureStack();
        EnsureMenu();

        if (MpMenuUI.Instance == null)
        {
            Debug.LogError("[MP] No MP menu available - run 'Tools → MP → Extract MP Menu Prefab' " +
                           "(or let the runtime menu build itself).");
            return;
        }

        MpMenuUI.Instance.ShowMultiplayerMenu();

        if (logActions)
            Debug.Log($"[MP] Multiplayer menu opened (stack={(stackReady ? "ready" : "MISSING")}).");
    }

    private bool EnsureStack()
    {
        if (FindAnyObjectByType<LobbyNetworkManager>(FindObjectsInactive.Include) != null)
            return true;

        if (stackPrefab != null)
        {
            GameObject stack = Instantiate(stackPrefab);
            stack.name = "MpStack(Instance)";
            if (logActions) Debug.Log("[MP] MP stack instantiated from the prefab.");
            return true;
        }

        Debug.LogError("[MP] This scene has no MP stack and no stackPrefab assigned. " +
                       "Run 'Tools → MP → Extract MP Stack Prefab' and assign the result to MpBootstrap.");
        return false;
    }

    private void EnsureMenu()
    {
        if (MpMenuUI.Instance != null) return;

        if (menuPrefab != null)
        {
            Instantiate(menuPrefab);

            if (MpMenuUI.Instance != null)
            {
                if (logActions) Debug.Log($"[MP] menu instantiated from prefab '{menuPrefab.name}'.");
                return;
            }

            Debug.LogWarning($"[MP] '{menuPrefab.name}' was instantiated but no MpMenuUI became active " +
                             "(is the prefab root inactive?) - using the runtime menu instead.");
        }
        else
        {
            if (logActions) Debug.Log("[MP] no menu prefab assigned - building the runtime menu.");
        }

        MpMenuUI.CreateRuntime();
    }
}
