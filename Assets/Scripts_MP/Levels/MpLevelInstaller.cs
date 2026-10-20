using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ItemScript;
using Mirror;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
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

        // MP keeps the level's authored SP HUD and overwrites its values instead of
        // drawing a second HUD from the player prefab. Point the SP managers at those
        // widgets so the values land in the objects the level already has.
        BindAuthoredPlayerHud(scene);

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

        // Runs after the bake was started on purpose: a puddle mesh that just became
        // active must not end up as walkable NavMesh source geometry.
        RepairSceneLogErrors(scene);

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
    ///
    /// The bake itself is now ASYNCHRONOUS. `surface.BuildNavMesh()` voxelizes the whole
    /// level on the main thread - measured at 7.5 s for Level 1 and 37.9 s for the
    /// Level 2 city - and that single call is what made an MP level look like it froze
    /// the editor on load. `surface.UpdateNavMesh` hands the same work to the NavMesh
    /// worker threads, so the level appears immediately and the dogs simply start
    /// patrolling once the bake reports done.
    ///
    /// Completed data is also cached per scene and kept REGISTERED in the navigation
    /// system across scene changes (the instance is owned by this DontDestroyOnLoad
    /// object, not by the surface, so the surface cannot remove it on unload). Loading
    /// the same level again - or coming back to it from the MP menu - therefore has a
    /// NavMesh from the first frame, which also removes the one-per-load
    /// "Failed to create agent ..." / "ResetPath ..." warning pair that the authored
    /// dog emits while no NavMesh exists yet.
    /// </summary>
    private sealed class CachedNavMesh
    {
        public NavMeshData data;
        public NavMeshDataInstance instance;
        public Vector3 position;
        public Quaternion rotation;
    }

    private static readonly Dictionary<string, CachedNavMesh> NavMeshCache =
        new Dictionary<string, CachedNavMesh>();

    private readonly HashSet<string> navMeshBuildsInFlight = new HashSet<string>();

    /// <summary>
    /// False while an asynchronous bake for the current level is still running. Dog /
    /// mail man AI has to wait for this: calling NavMeshAgent.SetDestination before the
    /// NavMesh exists logs an error EVERY frame, and the bake now takes seconds without
    /// blocking the main thread (it used to block, which hid the spam).
    /// </summary>
    public static bool NavMeshReady { get; private set; } = true;

    private static void SetNavMeshReady(bool ready)
    {
        NavMeshReady = ready;
    }

    private void EnsureNavMesh(Scene scene)
    {
        // Two levels at the same world coordinates must never share the navigation
        // system at once (an agent could snap onto the other level's NavMesh), so drop
        // this object's registration for any other level. The data stays cached.
        ReleaseOtherSceneNavMeshes(scene);

        foreach (NavMeshSurface surface in FindObjectsByType<NavMeshSurface>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (surface == null) continue;

            if (surface.navMeshData != null) continue;      // authored/baked data

            string key = NavMeshKey(scene, surface);

            if (NavMeshCache.TryGetValue(key, out CachedNavMesh cached) && cached.data != null)
            {
                AttachCachedNavMesh(surface, cached);
                PlaceAgentsOnNavMesh(scene);

                if (logInstalls)
                    InteractionDebug.Log($"reused cached NavMesh for '{scene.name}' " +
                                         $"(surface '{surface.name}')");

                continue;
            }

            if (!navMeshBuildsInFlight.Add(key)) continue;  // one build per surface

            SetNavMeshReady(false);
            StartCoroutine(BuildNavMeshAsync(scene, surface, key));
        }
    }

    private IEnumerator BuildNavMeshAsync(Scene scene, NavMeshSurface surface, string key)
    {
        Vector3 position = surface.transform.position;
        Quaternion rotation = surface.transform.rotation;
        string surfaceLabel = surface.name;

        // Seed the data with the surface transform so UpdateNavMeshDataAsync writes it
        // in the same space the surface uses (this mirrors what the package's own
        // editor baker does).
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            surface.GetBuildSettings(),
            new List<NavMeshBuildSource>(),
            new Bounds(),
            position,
            rotation);

        // CollectSources() runs synchronously inside UpdateNavMesh and emits hundreds of
        // "Source mesh ... does not allow read access" warnings for a big level. Walking
        // and writing a stack for each one buried the log, so mute warning stacks just
        // for that call; the expensive voxelization happens afterwards on worker threads.
        StackTraceLogType previous = Application.GetStackTraceLogType(LogType.Warning);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        AsyncOperation operation;
        try
        {
            operation = surface.UpdateNavMesh(data);
        }
        finally
        {
            Application.SetStackTraceLogType(LogType.Warning, previous);
        }

        if (operation == null)
        {
            navMeshBuildsInFlight.Remove(key);
            SetNavMeshReady(navMeshBuildsInFlight.Count == 0);
            Object.Destroy(data);
            yield break;
        }

        while (!operation.isDone) yield return null;

        stopwatch.Stop();
        navMeshBuildsInFlight.Remove(key);
        SetNavMeshReady(navMeshBuildsInFlight.Count == 0);

        // The level may have been unloaded while the worker threads were baking; in
        // that case re-find the surface only if the same scene is loaded again.
        NavMeshSurface target = surface != null ? surface : FindSurface(scene, key);
        bool sceneLoadedAgain = target != null || (scene.IsValid() && scene.isLoaded);

        CachedNavMesh entry = new CachedNavMesh
        {
            data = data,
            position = position,
            rotation = rotation,
        };

        // Register now when this level is still (or again) loaded; otherwise keep the
        // data cached for the next load so a different level's NavMesh is never live
        // at the same time.
        if (sceneLoadedAgain)
            entry.instance = NavMesh.AddNavMeshData(data, position, rotation);

        if (NavMeshCache.TryGetValue(key, out CachedNavMesh old) && old.data != null && old.data != data)
        {
            if (old.instance.valid) old.instance.Remove();
            Object.Destroy(old.data);
        }

        NavMeshCache[key] = entry;

        if (target != null)
        {
            // Keep the surface's own registration out of the way; the cached instance
            // above is the one that must survive this scene.
            target.RemoveData();
            target.navMeshData = data;
        }

        if (scene.IsValid() && scene.isLoaded)
            PlaceAgentsOnNavMesh(scene);

        if (logInstalls)
            InteractionDebug.Log($"built NavMesh for '{scene.name}' (surface '{surfaceLabel}') " +
                                 $"in {stopwatch.ElapsedMilliseconds} ms (async)");
    }

    private static NavMeshSurface FindSurface(Scene scene, string key)
    {
        foreach (NavMeshSurface candidate in FindObjectsByType<NavMeshSurface>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;
            if (NavMeshKey(scene, candidate) == key) return candidate;
        }

        return null;
    }

    private static void AttachCachedNavMesh(NavMeshSurface surface, CachedNavMesh cached)
    {
        if (!cached.instance.valid)
        {
            cached.instance = NavMesh.AddNavMeshData(
                cached.data, cached.position, cached.rotation);
        }

        // Hand the data to the surface for bookkeeping, but do NOT call AddData():
        // the registration above is owned by this installer and has to survive the
        // surface being destroyed with the scene.
        surface.navMeshData = cached.data;
    }

    private static void ReleaseOtherSceneNavMeshes(Scene scene)
    {
        string prefix = scene.path + "|";

        foreach (KeyValuePair<string, CachedNavMesh> pair in NavMeshCache)
        {
            if (pair.Key.StartsWith(prefix)) continue;
            if (!pair.Value.instance.valid) continue;

            pair.Value.instance.Remove();
            pair.Value.instance = default;
        }
    }

    /// <summary>
    /// Agents enabled before a NavMesh existed stay unplaced for the rest of the
    /// session. Re-creating them now that the bake is done puts them back on the mesh;
    /// disabled agents (ghost dogs stopped by DogSync_MP) keep their disabled state.
    /// </summary>
    private static void PlaceAgentsOnNavMesh(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent == null || !agent.isActiveAndEnabled || agent.isOnNavMesh) continue;

                agent.enabled = false;
                agent.enabled = true;

                if (!agent.isOnNavMesh &&
                    NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit,
                                           3f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }
        }
    }

    private static string NavMeshKey(Scene scene, NavMeshSurface surface)
    {
        return scene.path + "|" + PathOf(surface.transform) + "|" + surface.agentTypeID;
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

    // ---------------------------------------------------------------- authored SP HUD

    /// <summary>
    /// Finds the player HUD the SP level already contains and binds the SP managers to
    /// its widgets. Nothing is disabled or removed: the authored money/time objects stay
    /// exactly where they are, the SP CurrencyManager writes the network-synced wallet
    /// into them and GameTimeManager's UpdateUI writes the synchronized round time.
    /// </summary>
    private void BindAuthoredPlayerHud(Scene scene)
    {
        CurrencyText currencyText = null;
        TextMeshProUGUI timerText = null;
        string packagePath = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (PlayerStateMachine stateMachine in root.GetComponentsInChildren<PlayerStateMachine>(true))
            {
                Transform avatar = stateMachine.transform;

                // Mirror player prefabs are not the authored level objects.
                if (avatar.GetComponentInParent<NetworkIdentity>(true) != null) continue;

                GameObject package = FindPlayerPackage(avatar);
                if (package == null || !package.scene.IsValid() || !package.activeInHierarchy) continue;

                currencyText = package.GetComponentInChildren<CurrencyText>(true);
                timerText = FindTimeCounterText(package.transform);
                packagePath = PathOf(package.transform);

                PrepareAuthoredPlayer(package);
                break;
            }

            if (packagePath != null) break;
        }

        MpSharedCompat.BindAuthoredPlayerHud(currencyText, timerText);

        if (packagePath == null || !logInstalls) return;

        InteractionDebug.Log($"using authored SP HUD '{packagePath}' " +
                             $"(money={(currencyText != null)}, time={(timerText != null)})");
    }

    /// <summary>
    /// The level keeps its authored SP player package because the money/time HUD lives
    /// on it, but the PLAYER itself belongs to the Mirror prefab. This turns the
    /// authored package into an HUD-only object:
    ///
    ///  - the duplicate avatar / dog / mail man / cameras are switched off, so an MP
    ///    level no longer shows the authored player standing next to the networked one
    ///    ("two players"),
    ///  - the authored PauseMenu is switched off because it pauses the whole world via
    ///    GamePauseManager (the Mirror player prefab has an owner-only PauseMenu),
    ///  - the authored camera + AudioListener + CinemachineBrain are silenced so the
    ///    level is not rendered a second time (that double render pinned the CPU).
    ///
    /// The package root, its OverlayCanvas (the HUD MP writes into) and its
    /// BoundCollider stay untouched.
    /// </summary>
    private void PrepareAuthoredPlayer(GameObject package)
    {
        SilenceAuthoredCamera(package);
        DisableAuthoredDuplicates(package);
        DisableAuthoredPauseMenu(package);
    }

    private static readonly string[] AuthoredPlayerDuplicates =
    {
        "Player_Default", "DogPackage", "MailManPackage", "LevelStartCamera", "InLevelCamera",
    };

    private void DisableAuthoredDuplicates(GameObject package)
    {
        foreach (string childName in AuthoredPlayerDuplicates)
        {
            Transform child = package.transform.Find(childName);
            if (child == null || !child.gameObject.activeSelf) continue;

            child.gameObject.SetActive(false);

            if (logInstalls)
                InteractionDebug.Log($"MP: disabled authored duplicate '{childName}' " +
                                     "(the networked player prefab owns this object)");
        }
    }

    private void DisableAuthoredPauseMenu(GameObject package)
    {
        foreach (PauseMenu pauseMenu in package.GetComponentsInChildren<PauseMenu>(true))
        {
            if (!pauseMenu.enabled) continue;

            pauseMenu.enabled = false;

            if (logInstalls)
                InteractionDebug.Log("MP: authored PauseMenu disabled (the MP player prefab has its own)");
        }
    }

    /// <summary>
    /// The authored SP package keeps its whole player stack - including a Main Camera
    /// with an AudioListener - while the MP player prefab spawns the same camera again
    /// inside LocalPlayerOnly. With both enabled, Unity renders the entire level twice
    /// every frame (and warns about two audio listeners), which is what made big MP
    /// levels like Level 2 pin the CPU. Only the rendering/audio output is silenced;
    /// the package itself and the HUD we write into stay untouched.
    /// </summary>
    private static void SilenceAuthoredCamera(GameObject package)
    {
        foreach (Camera camera in package.GetComponentsInChildren<Camera>(true))
            camera.enabled = false;

        foreach (AudioListener listener in package.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        // The brain keeps evaluating the authored virtual cameras even when its
        // output Camera is off; the MP player has its own brain.
        foreach (Unity.Cinemachine.CinemachineBrain brain in
                 package.GetComponentsInChildren<Unity.Cinemachine.CinemachineBrain>(true))
            brain.enabled = false;
    }

    private static TextMeshProUGUI FindTimeCounterText(Transform package)
    {
        foreach (TextMeshProUGUI text in package.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.transform.parent != null && text.transform.parent.name == "TimeCounter")
                return text;
        }

        return null;
    }

    private static GameObject FindPlayerPackage(Transform avatar)
    {
        for (Transform candidate = avatar; candidate != null; candidate = candidate.parent)
        {
            if (candidate.GetComponentInChildren<CurrencyText>(true) != null)
                return candidate.gameObject;
        }

        return null;
    }

    // ---------------------------------------------------------------- scene log repairs

    private static readonly FieldInfo CarSpawnPointsField =
        typeof(CarController_Prototype).GetField(
            "spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Two authoring mistakes in the levels throw at runtime (they do so in SP as
    /// well, but SP never had a log to watch). Repairing them here keeps an MP
    /// session's console to actual network problems:
    ///
    ///  - Level 2's CarController_Prototype has an empty lane in its spawnPoints
    ///    array; the car routine picks lanes at random and dereferences the null
    ///    Transform -> UnassignedReferenceException every time it picks that lane,
    ///  - the rain event calls StartLerping on a dissolve helper that sits on the
    ///    authored-inactive 'SceneWaterPuddle' -> "Coroutine couldn't be started
    ///    because the game object ... is inactive" on every rain cycle.
    /// </summary>
    private void RepairSceneLogErrors(Scene scene)
    {
        int repairedCars = 0;
        int activatedLerpObjects = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            if (CarSpawnPointsField != null)
            {
                foreach (CarController_Prototype car in
                         root.GetComponentsInChildren<CarController_Prototype>(true))
                {
                    if (!(CarSpawnPointsField.GetValue(car) is Transform[] points)) continue;

                    int valid = 0;
                    foreach (Transform point in points)
                        if (point != null) valid++;

                    if (valid == points.Length) continue;

                    Transform[] cleaned = new Transform[valid];
                    int index = 0;
                    foreach (Transform point in points)
                        if (point != null) cleaned[index++] = point;

                    CarSpawnPointsField.SetValue(car, cleaned);
                    repairedCars++;
                }
            }

            foreach (MaterialParameterLerpHelper helper in
                     root.GetComponentsInChildren<MaterialParameterLerpHelper>(true))
            {
                GameObject target = helper.gameObject;

                // Only objects that a parent hierarchy would activate anyway; leaving an
                // intentionally disabled branch alone is safer than switching it on.
                if (target.activeInHierarchy || target.activeSelf) continue;
                if (target.transform.parent != null &&
                    !target.transform.parent.gameObject.activeInHierarchy) continue;
                if (target.GetComponent<MeshRenderer>() == null) continue;

                // The helper starts at lerp 0, which is fully dissolved, so the object
                // stays invisible until the rain event fades it in.
                target.SetActive(true);
                activatedLerpObjects++;
            }
        }

        if (logInstalls && (repairedCars > 0 || activatedLerpObjects > 0))
            InteractionDebug.Log($"repaired scene log errors in '{scene.name}' " +
                                 $"(car spawn points: {repairedCars}, " +
                                 $"inactive dissolve objects: {activatedLerpObjects})");
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
