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

public class MpLevelInstaller : MonoBehaviour
{
    public static MpLevelInstaller Instance { get; private set; }

    [Tooltip("Scenes that must never be MP-installed (menus).")]
    [SerializeField] private string[] ignoredSceneNames = { "MenuScene", "MenuScene_MP" };

    [Tooltip("Install into the scene currently loaded when the game starts, too.")]
    [SerializeField] private bool installOnStart = true;

    [Tooltip("Log every object that was made multiplayer-ready.")]
    [SerializeField] private bool logInstalls = true;

    [Header("Runtime NavMesh (sadece piserilmemis sahneler icin)")]
    [Tooltip("Piserilmis NavMesh yoksa runtime bake'i kaba (hizli) ayarlarla yap.")]
    [SerializeField] private bool coarseRuntimeBake = true;

    [Tooltip("Kaba bake voxel boyutu. Buyuk = hizli/kaba. 0.17 varsayilan, 0.32 yaklasik 3.5x hizli.")]
    [SerializeField] private float coarseVoxelSize = 0.32f;

    [Tooltip("Bake alanini zemin sinirlariyla sinirla (tum sahne yerine).")]
    [SerializeField] private bool limitBakeArea = true;

    [Tooltip("NavMesh'i senkron bake et. Boylece agent'lar (kopek/postaci) NavMesh hazirken olusur ve 'agent not placed' hatalari cikmaz.")]
    [SerializeField] private bool synchronousRuntimeBake = true;

    [Header("Otomatik bolgeler (sahnede elle ZoneVolume yoksa)")]
    [Tooltip("Otomatik olusturulan PlayArea'nin kenar politikasi. None = oyuncuyu geri itmez.")]
    [SerializeField] private BoundaryPolicy autoPlayAreaPolicy = BoundaryPolicy.None;

    [Tooltip("Otomatik PlayArea icin zemin sinirlarina eklenecek marj (metre).")]
    [SerializeField] private float autoPlayAreaMargin = 12f;

    private static readonly (System.Type spi, System.Type[] added, string label, bool skipInsideStation)[] Rules =
    {
        (typeof(Cutter),             new[] { typeof(Cutter_MP) },                                       "cutter",      false),
        (typeof(ObjectSpawner),      new[] { typeof(Spawner_MP) },                                      "spawner",     false),
        (typeof(Mixer),              new[] { typeof(Mixer_MP) },                                        "mixer",       false),
        (typeof(Constructor),        new[] { typeof(Constructor_MP) },                                  "constructor", false),
        (typeof(CarriableObject_SP), new[] { typeof(CarriableObject_MP), typeof(NetworkSpawnSelf_MP) },  "carriable",   false),
        (typeof(Shop),               new[] { typeof(Shop_MP) },                                         "shop",        false),
        (typeof(ShopIndex),          new[] { typeof(ShopIndex_MP) },                                    "shop-index",  false),
        (typeof(Generator_SP),       new[] { typeof(Generator_MP) },                                    "generator",   true),
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

    public int Install(Scene scene)
    {
        if (!NetworkServer.active && !NetworkClient.active) return 0;
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return 0;
        if (IsIgnored(scene)) return 0;

        BindAuthoredPlayerHud(scene);

        MpEndUiSuppressor_MP.Suppress(scene);

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
        EnsureLevelData(scene);

        RepairSceneLogErrors(scene);

        if (installed > 0)
        {
            Debug.Log($"[MP-INSTALL] '{scene.name}': {installed} object(s) made multiplayer-ready " +
                      $"(server={NetworkServer.active}, client={NetworkClient.active}).");

            if (NetworkServer.active) SpawnServerObjects();
        }

        return installed;
    }

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

    public static bool NavMeshReady { get; private set; } = true;

    private static void SetNavMeshReady(bool ready)
    {
        NavMeshReady = ready;
    }

    private void EnsureNavMesh(Scene scene)
    {
        ReleaseOtherSceneNavMeshes(scene);

        foreach (NavMeshSurface surface in FindObjectsByType<NavMeshSurface>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (surface == null) continue;

            if (surface.navMeshData != null) continue;

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

            if (synchronousRuntimeBake)
            {
                BakeNavMeshNow(scene, surface);
                continue;
            }

            if (!navMeshBuildsInFlight.Add(key)) continue;

            SetNavMeshReady(false);
            StartCoroutine(BuildNavMeshAsync(scene, surface, key));
        }
    }

    private void ApplyRuntimeBakeSettings(Scene scene, NavMeshSurface surface)
    {
        if (coarseRuntimeBake)
        {
            surface.overrideVoxelSize = true;
            surface.voxelSize = Mathf.Max(surface.voxelSize, coarseVoxelSize);
        }

        if (!limitBakeArea) return;
        if (!TryGetBakeArea(scene, out Bounds bakeArea)) return;

        surface.collectObjects = CollectObjects.Volume;
        surface.center = bakeArea.center;
        surface.size = bakeArea.size;
    }

    private void BakeNavMeshNow(Scene scene, NavMeshSurface surface)
    {
        ApplyRuntimeBakeSettings(scene, surface);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        StackTraceLogType previous = Application.GetStackTraceLogType(LogType.Warning);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        try
        {
            surface.BuildNavMesh();
        }
        finally
        {
            Application.SetStackTraceLogType(LogType.Warning, previous);
        }

        stopwatch.Stop();

        SetNavMeshReady(navMeshBuildsInFlight.Count == 0);
        PlaceAgentsOnNavMesh(scene);

        if (logInstalls)
        {
            InteractionDebug.Log($"NavMesh ({scene.name} / {surface.name}) senkron bake edildi: " +
                                 $"{stopwatch.ElapsedMilliseconds} ms, voxel={surface.voxelSize:0.###}, " +
                                 $"collect={surface.collectObjects}, area={surface.size}");
        }
    }

    private IEnumerator BuildNavMeshAsync(Scene scene, NavMeshSurface surface, string key)
    {
        Vector3 position = surface.transform.position;
        Quaternion rotation = surface.transform.rotation;
        string surfaceLabel = surface.name;

        ApplyRuntimeBakeSettings(scene, surface);

        NavMeshBuildSettings settings = surface.GetBuildSettings();

        if (logInstalls)
        {
            InteractionDebug.Log($"runtime NavMesh bake ayarlari ('{scene.name}' / '{surfaceLabel}'): " +
                                 $"voxel={settings.voxelSize:0.###}, agentRadius={settings.agentRadius:0.##}, " +
                                 $"collect={surface.collectObjects}, area={surface.size}");
        }

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            settings,
            new List<NavMeshBuildSource>(),
            new Bounds(),
            position,
            rotation);

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

        NavMeshSurface target = surface != null ? surface : FindSurface(scene, key);
        bool sceneLoadedAgain = target != null || (scene.IsValid() && scene.isLoaded);

        CachedNavMesh entry = new CachedNavMesh
        {
            data = data,
            position = position,
            rotation = rotation,
        };

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
            target.RemoveData();
            target.navMeshData = data;
        }

        if (scene.IsValid() && scene.isLoaded)
        {
            PlaceAgentsOnNavMesh(scene);
            StartCoroutine(RepositionAgentsLater(scene));
        }

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

    private const float AgentPlacementRadius = 15f;

    private static int PlaceAgentsOnNavMesh(Scene scene)
    {
        int placed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent == null || !agent.isActiveAndEnabled || agent.isOnNavMesh) continue;

                agent.enabled = false;
                agent.enabled = true;

                if (agent.isOnNavMesh) continue;

                if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit,
                                            AgentPlacementRadius, NavMesh.AllAreas))
                {
                    continue;
                }

                agent.Warp(hit.position);
                placed++;
            }
        }

        return placed;
    }

    private IEnumerator RepositionAgentsLater(Scene scene)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            yield return new WaitForSeconds(0.5f);

            if (!scene.IsValid() || !scene.isLoaded) yield break;

            int placed = PlaceAgentsOnNavMesh(scene);
            if (placed <= 0) continue;

            if (logInstalls)
                InteractionDebug.Log($"'{scene.name}': {placed} agent NavMesh'e yerlestirildi (deneme {attempt + 1}).");
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
        if (go.GetComponent<NetworkIdentity>() != null) return false;
        if (go.GetComponentInParent<NetworkIdentity>(true) != null) return false;

        if (!go.scene.IsValid()) return false;

        foreach ((System.Type spi, System.Type[] added, string label, bool skipInsideStation) in Rules)
        {
            if (go.GetComponent(spi) == null) continue;

            if (skipInsideStation &&
                (go.GetComponentInParent<Cutter>(true) != null || go.GetComponentInParent<Mixer>(true) != null))
            {
                continue;
            }

            foreach (System.Type type in added)
            {
                if (go.GetComponent(type) == null) go.AddComponent(type);
            }

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

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
            if (identity.netId != 0) continue;

            NetworkServer.Spawn(identity.gameObject);
        }
    }

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

    private static void SilenceAuthoredCamera(GameObject package)
    {
        foreach (Camera camera in package.GetComponentsInChildren<Camera>(true))
            camera.enabled = false;

        foreach (AudioListener listener in package.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

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

    private static readonly FieldInfo CarSpawnPointsField =
        typeof(CarController_Prototype).GetField(
            "spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic);

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

                if (target.activeInHierarchy || target.activeSelf) continue;
                if (target.transform.parent != null &&
                    !target.transform.parent.gameObject.activeInHierarchy) continue;
                if (target.GetComponent<MeshRenderer>() == null) continue;

                target.SetActive(true);
                activatedLerpObjects++;
            }
        }

        if (logInstalls && (repairedCars > 0 || activatedLerpObjects > 0))
            InteractionDebug.Log($"repaired scene log errors in '{scene.name}' " +
                                 $"(car spawn points: {repairedCars}, " +
                                 $"inactive dissolve objects: {activatedLerpObjects})");
    }

    private static ulong ComposeSceneId(GameObject go)
    {
        uint objectHash = Fnv1a(PathOf(go.transform));
        if (objectHash == 0) objectHash = 1;

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

    private void EnsureLevelData(Scene scene)
    {
        Bounds bounds = GroundBounds(scene);
        bool hasBounds = bounds.size.sqrMagnitude > 0.001f;

        if (FindInScene(scene, "SpawnPoint") == null)
        {
            Vector3 position = hasBounds
                ? new Vector3(bounds.center.x, bounds.min.y + 1f, bounds.center.z)
                : Vector3.zero;

            var spawn = new GameObject("SpawnPoint");
            spawn.transform.position = position;

            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f))
                spawn.transform.position = hit.point;

            InteractionDebug.Log($"created SpawnPoint in '{scene.name}' at {spawn.transform.position}");
        }

        if (!hasBounds) return;

        if (ZoneRegistry.PrimaryPlayArea() == null)
        {
            Bounds play = Grow(bounds, autoPlayAreaMargin);
            CreateZone("Zone_PlayArea", play, ZoneKind.PlayArea, true);
            InteractionDebug.Log($"created Zone_PlayArea in '{scene.name}' (center {play.center}, size {play.size}, policy {autoPlayAreaPolicy})");
        }

        if (ZoneRegistry.Nearest(bounds.center, ZoneKind.DogPatrol) == null)
        {
            Bounds patrol = Shrink(bounds, 0.35f);
            CreateZone("Zone_DogPatrol", patrol, ZoneKind.DogPatrol, false);
            InteractionDebug.Log($"created Zone_DogPatrol in '{scene.name}' (center {patrol.center}, size {patrol.size})");
        }
    }

    private void CreateZone(string name, Bounds bounds, ZoneKind kind, bool primary)
    {
        var go = new GameObject(name);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = bounds.center;
        box.size = bounds.size;

        ZoneVolume zone = go.AddComponent<ZoneVolume>();

        BoundaryPolicy boundaryPolicy = kind == ZoneKind.PlayArea
            ? autoPlayAreaPolicy
            : BoundaryPolicy.None;

        string zoneId = kind == ZoneKind.DogPatrol ? "dog_patrol" : null;

        zone.Configure(kind, primary, boundaryPolicy, zoneId);
    }

    private bool TryGetBakeArea(Scene scene, out Bounds area)
    {
        area = GroundBounds(scene);

        if (area.size.sqrMagnitude <= 1f) return false;

        area.size = new Vector3(
            area.size.x * 1.1f,
            Mathf.Max(24f, area.size.y),
            area.size.z * 1.1f);

        return true;
    }

    private static Bounds BoundsOfAllRenderers(Scene scene)
    {
        Bounds all = new Bounds(Vector3.zero, Vector3.zero);
        bool hasAny = false;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;

                if (!hasAny) { all = renderer.bounds; hasAny = true; }
                else all.Encapsulate(renderer.bounds);
            }
        }

        return all;
    }

    private static Bounds GroundBounds(Scene scene)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        bool has = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (groundLayer >= 0 && renderer.gameObject.layer != groundLayer) continue;

                if (!has) { bounds = renderer.bounds; has = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }

        bool tooThin = has && Mathf.Min(bounds.size.x, bounds.size.z) < Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f;

        bool tooSmall = false;

        if (has)
        {
            Bounds allExtent = BoundsOfAllRenderers(scene);

            if (allExtent.size.sqrMagnitude > 1f)
            {
                tooSmall = bounds.size.x < allExtent.size.x * 0.7f ||
                           bounds.size.z < allExtent.size.z * 0.7f;
            }
        }

        if (!has || tooThin || tooSmall)
        {
            Bounds all = new Bounds(Vector3.zero, Vector3.zero);
            bool hasAll = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hasAll) { all = renderer.bounds; hasAll = true; }
                    else all.Encapsulate(renderer.bounds);
                }
            }

            if (hasAll) bounds = all;
        }

        bounds.size = new Vector3(bounds.size.x, Mathf.Max(20f, bounds.size.y), bounds.size.z);
        return bounds;
    }

    private static Bounds Grow(Bounds b, float margin)
    {
        b.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
        return b;
    }

    private static Bounds Shrink(Bounds b, float factor)
    {
        b.size = new Vector3(b.size.x * (1f - factor), b.size.y, b.size.z * (1f - factor));
        return b;
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = root.transform.Find(name);
            if (found != null) return found.gameObject;
        }

        return null;
    }
}
