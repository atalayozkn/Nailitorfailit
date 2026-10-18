using System.Collections.Generic;
using ItemScript;
using Mirror;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes ANY level scene multiplayer-ready at load time (M4 of the modular MP system).
///
/// Until now a level only worked in MP if its scene had been opened, re-saved and
/// hand-patched with NetworkIdentities + sceneIds (that is why several sessions got
/// stuck on "scene needs to be opened and resaved"). This installer removes that
/// requirement completely: on every machine, right after a level is loaded, it walks
/// the scene and applies a small rule table:
///
///     Cutter              -> + NetworkIdentity + Cutter_MP
///     ObjectSpawner       -> + NetworkIdentity + Spawner_MP
///     Mixer               -> + NetworkIdentity + Mixer_MP           (handles the crank)
///     Constructor         -> + NetworkIdentity + Constructor_MP     (handles the tiles)
///     CarriableObject_SP  -> + NetworkIdentity + CarriableObject_MP + NetworkSpawnSelf_MP
///
/// The sceneId is derived DETERMINISTICALLY from the object's hierarchy path, so the
/// server and every client compute the same value and Mirror's normal scene-object
/// handshake does the rest - no scene editing, no prefab surgery.
///
/// Supporting a new system in MP = adding one row to <see cref="Rules"/>.
/// </summary>
public class MpLevelInstaller : MonoBehaviour
{
    public static MpLevelInstaller Instance { get; private set; }

    [Tooltip("Scenes that must never be MP-installed (menus).")]
    [SerializeField] private string[] ignoredSceneNames = { "MenuScene", "MenuScene_MP" };

    [Tooltip("Install into the scene currently loaded when the game starts, too.")]
    [SerializeField] private bool installOnStart = true;

    [Tooltip("Log every object that was made multiplayer-ready.")]
    [SerializeField] private bool logInstalls = true;

    /// <summary>
    /// SP component -> components to add next to the (implicitly added) NetworkIdentity.
    /// </summary>
    private static readonly (System.Type spi, System.Type[] added, string label)[] Rules =
    {
        (typeof(Cutter),             new[] { typeof(Cutter_MP) },                                              "cutter"),
        (typeof(ObjectSpawner),      new[] { typeof(Spawner_MP) },                                             "spawner"),
        (typeof(Mixer),              new[] { typeof(Mixer_MP) },                                               "mixer"),
        (typeof(Constructor),        new[] { typeof(Constructor_MP) },                                         "constructor"),
        (typeof(CarriableObject_SP), new[] { typeof(CarriableObject_MP), typeof(NetworkSpawnSelf_MP) },         "carriable"),
    };

    private readonly List<NetworkIdentity> addedThisScene = new List<NetworkIdentity>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpLevelInstaller>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpLevelInstaller").AddComponent<MpLevelInstaller>();
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
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (installOnStart) Install(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install(scene);
    }

    // ---------------------------------------------------------------- install pass

    public int Install(Scene scene)
    {
        // Singleplayer sessions must stay completely untouched.
        if (!NetworkServer.active && !NetworkClient.active) return 0;
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return 0;
        if (IsIgnored(scene)) return 0;

        addedThisScene.Clear();

        int installed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (TryInstallObject(t.gameObject)) installed++;
            }
        }

        EnsureNavMesh(scene);

        if (installed > 0)
        {
            Debug.Log($"[MP-INSTALL] '{scene.name}': {installed} object(s) made multiplayer-ready " +
                      $"(server={NetworkServer.active}, client={NetworkClient.active}).");

            // Mirror registers scene objects at the start of its spawn phase; anything we
            // added after that must be spawned explicitly (server only, and never twice:
            // NetworkServer.Spawn on an already spawned netId is skipped below).
            if (NetworkServer.active) SpawnServerObjects();
        }

        return installed;
    }

    /// <summary>
    /// The levels have no baked NavMesh - a NavMeshSurface builds one at RUNTIME, often
    /// after the dog / mail man agents have already spawned, which produced
    ///     '"SetDestination" can only be called on an active agent that has been placed
    ///      on a NavMesh.'
    /// Building it here removes the race (and costs a few ms per level load).
    /// </summary>
    private void EnsureNavMesh(Scene scene)
    {
        foreach (NavMeshSurface surface in FindObjectsByType<NavMeshSurface>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (surface == null) continue;
            if (surface.navMeshData != null) continue;      // already baked

            surface.BuildNavMesh();

            if (logInstalls)
                InteractionDebug.Log($"built NavMesh for '{scene.name}' (surface '{surface.name}')");
        }
    }

    private bool IsIgnored(Scene scene)
    {
        if (ignoredSceneNames == null) return false;

        foreach (string ignored in ignoredSceneNames)
        {
            if (!string.IsNullOrEmpty(ignored) && scene.name.Contains(ignored)) return true;
        }

        return false;
    }

    private bool TryInstallObject(GameObject go)
    {
        // already networked (authored identity, or a child of one)? leave it alone.
        if (go.GetComponent<NetworkIdentity>() != null) return false;
        if (go.GetComponentInParent<NetworkIdentity>(true) != null) return false;

        // prefab assets / invalid scenes
        if (!go.scene.IsValid()) return false;

        foreach ((System.Type spi, System.Type[] added, string label) in Rules)
        {
            if (go.GetComponent(spi) == null) continue;

            foreach (System.Type type in added)
            {
                if (go.GetComponent(type) == null) go.AddComponent(type);
            }

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            // deterministic id: same value on the server and on every client.
            // Layout mirrors Mirror's scene objects: [scene path hash | object hash].
            identity.sceneId = ComposeSceneId(go);

            addedThisScene.Add(identity);

            if (logInstalls)
            {
                InteractionDebug.Log($"installed MP for [{label}] '{PathOf(go.transform)}' " +
                                     $"sceneId={identity.sceneId:X16}");
            }

            return true;
        }

        return false;
    }

    private void SpawnServerObjects()
    {
        foreach (NetworkIdentity identity in addedThisScene)
        {
            if (identity == null) continue;
            if (identity.netId != 0) continue;              // already spawned

            NetworkServer.Spawn(identity.gameObject);
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Full sceneId for a runtime-created scene object: low 32 bits identify the object
    /// (hash of its hierarchy path), high 32 bits identify the scene (hash of its lower
    /// cased path - Mirror does the same to keep ids stable between editor and build).
    ///
    /// Both parts are computed locally on purpose: Mirror's own helper for the hash part
    /// is not accessible from this assembly, and the only requirement is that the server
    /// and every client compute the SAME value - which this does.
    /// </summary>
    private static ulong ComposeSceneId(GameObject go)
    {
        uint objectHash = Fnv1a(PathOf(go.transform));
        if (objectHash == 0) objectHash = 1;   // 0 means "no scene id"

        string scenePath = go.scene.path;
        uint sceneHash = string.IsNullOrEmpty(scenePath) ? 0 : Fnv1a(scenePath.ToLowerInvariant());

        return ((ulong)sceneHash << 32) | objectHash;
    }

    private static uint Fnv1a(string text)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        uint hash = offset;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;

        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }
}
