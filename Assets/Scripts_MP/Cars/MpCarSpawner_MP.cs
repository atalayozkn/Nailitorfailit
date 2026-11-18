using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MpCarSpawner_MP : MonoBehaviour
{
    [Tooltip("Leave empty to use LobbyNetworkManager.trafficCarPrefab.")]
    [SerializeField] private GameObject carPrefab;

    [Tooltip("Seconds between two spawns.")]
    [SerializeField] private float interval = 2f;

    [Tooltip("A lane is skipped when a live car is within this distance of its point.")]
    [SerializeField] private float laneClearRadius = 3f;

    [Tooltip("Log bind/spawn/despawn decisions.")]
    [SerializeField] private bool logActions = true;

    [Tooltip("How many cars may be alive at the same time.")]
    [SerializeField] private int maxConcurrent = 4;

    private static readonly FieldInfo SpawnPointsField =
        typeof(CarController_Prototype).GetField("spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic);

    // Level 5 (and others) can have an oil truck: a networked truck that spills an oil strip behind it (OilTruckSpiller
    // runs on every machine, the strip is a local decal + slip trigger). Interval like the SP CarController.
    private const string TruckResourceName = "Car_Active_OilTruck_MP";
    private static readonly FieldInfo OilTruckPrefabField =
        typeof(CarController_Prototype).GetField("oilTruckPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo OilTruckIntervalField =
        typeof(CarController_Prototype).GetField("oilTruckInterval", BindingFlags.Instance | BindingFlags.NonPublic);

    private static GameObject truckResourcesPrefab;
    private bool sceneHasOilTruck;
    private Vector2 truckInterval = new Vector2(25f, 40f);
    private float nextTruckAt;
    private GameObject liveTruck;

    // Opposite lanes that sit (almost) on the same line made cars from both directions drive through each other:
    // Level 4 has the two lanes 0.1 m apart, Level 2 and 5 only 1.2 m. Each lane is nudged sideways (to the right of
    // its own direction, i.e. right-hand traffic) until the two directions are at least this far apart.
    private const float OppositeLaneGap = 2.8f;
    private readonly Dictionary<Transform, Vector3> laneShift = new();

    private readonly List<Transform> lanes = new();
    private readonly List<GameObject> liveCars = new();

    private Scene boundScene;
    private float nextSpawnAt;
    private int spawnedTotal;
    private bool prefabWarningLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpCarSpawner_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpCarSpawner_MP").AddComponent<MpCarSpawner_MP>();
    }

    private const string ResourceName = "Car_Active_MP";

    private static GameObject resourcesPrefab;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        ResolvePrefab();
    }

    public static void RegisterPrefabs()
    {
        MpCarSpawner_MP spawner = FindAnyObjectByType<MpCarSpawner_MP>(FindObjectsInactive.Include);

        if (spawner == null)
        {
            InteractionDebug.Log("[MP-CAR] no spawner alive - cannot register the car prefab.");
            return;
        }

        GameObject prefab = spawner.ResolvePrefab();
        spawner.ResolveTruckPrefab(); // clients must know the truck before the server spawns one

        InteractionDebug.Log($"[MP-CAR] prefab registration refreshed " +
                             $"(prefab={(prefab != null ? prefab.name : "MISSING")}, " +
                             $"server={NetworkServer.active}, client={NetworkClient.active}).");
    }

    private void Update()
    {
        if (!NetworkServer.active && !NetworkClient.active) return;

        Scene active = SceneManager.GetActiveScene();
        if (active != boundScene) BindScene(active);

        if (!NetworkServer.active || lanes.Count == 0) return;
        if (Time.unscaledTime < nextSpawnAt) return;

        nextSpawnAt = Time.unscaledTime + Mathf.Max(0.5f, interval);
        SpawnOne(ResolvePrefab());

        if (sceneHasOilTruck && liveTruck == null && Time.unscaledTime >= nextTruckAt)
        {
            GameObject truck = ResolveTruckPrefab();
            if (truck != null && SpawnOne(truck, ignoreLimit: true) is GameObject spawned)
            {
                liveTruck = spawned;
                nextTruckAt = Time.unscaledTime + Random.Range(truckInterval.x, truckInterval.y);
            }
        }
    }

    private void BindScene(Scene scene)
    {
        boundScene = scene;
        lanes.Clear();
        roadSlabs = null;
        liveCars.Clear();

        DisableAuthoredTraffic(scene);
        CollectLanes(scene);
        DetectOilTruck(scene);

        nextSpawnAt = Time.unscaledTime + Mathf.Max(0.5f, interval);

        if (logActions)
            InteractionDebug.Log($"[MP-CAR] spawner bound to '{scene.name}' " +
                                 $"(lanes={lanes.Count}, prefab={DescribePrefab()})");
    }

    private void DisableAuthoredTraffic(Scene scene)
    {
        if (scene.IsValid())
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CarController_Prototype controller in
                         root.GetComponentsInChildren<CarController_Prototype>(true))
                {
                    controller.StopAllCoroutines();
                    controller.enabled = false;

                    int silenced = 0;

                    foreach (Car_Active pooled in controller.GetComponentsInChildren<Car_Active>(true))
                    {
                        if (pooled == null || !pooled.gameObject.activeSelf) continue;

                        pooled.gameObject.SetActive(false);
                        silenced++;
                    }

                    if (logActions)
                        InteractionDebug.Log($"[MP-CAR] local traffic '{controller.name}' disabled " +
                                             $"(stopped coroutines, silenced {silenced} pooled car(s)) - " +
                                             "the server spawns the networked cars.");
                }

                foreach (Car_Active scattered in root.GetComponentsInChildren<Car_Active>(true))
                {
                    if (scattered == null) continue;
                    if (scattered.GetComponent<NetworkIdentity>() != null) continue;
                    if (!scattered.gameObject.activeSelf) continue;

                    scattered.gameObject.SetActive(false);
                }
            }
        }

        foreach (MpTrafficSync_MP legacy in FindObjectsByType<MpTrafficSync_MP>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (!legacy.enabled) continue;

            legacy.enabled = false;

            if (logActions)
                InteractionDebug.Log("[MP-CAR] MpTrafficSync_MP disabled (MpCarSpawner_MP owns the traffic now).");
        }
    }

    private void DetectOilTruck(Scene scene)
    {
        sceneHasOilTruck = false;
        liveTruck = null;
        if (!scene.IsValid() || OilTruckPrefabField == null) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CarController_Prototype controller in root.GetComponentsInChildren<CarController_Prototype>(true))
            {
                if (OilTruckPrefabField.GetValue(controller) as GameObject == null) continue;

                sceneHasOilTruck = true;
                if (OilTruckIntervalField?.GetValue(controller) is Vector2 range) truckInterval = range;
                nextTruckAt = Time.unscaledTime + Random.Range(truckInterval.x, truckInterval.y);

                if (logActions) InteractionDebug.Log($"[MP-CAR] oil truck enabled for '{scene.name}' (every {truckInterval.x:0}-{truckInterval.y:0} s).");
                return;
            }
        }
    }

    private GameObject ResolveTruckPrefab()
    {
        if (truckResourcesPrefab == null) truckResourcesPrefab = Resources.Load<GameObject>(TruckResourceName);
        return truckResourcesPrefab != null ? RegisterIfNeeded(truckResourcesPrefab) : null;
    }

    // A level whose CarController has no spawn points assigned (Level 5 ships like that) still has its lane markers
    // under "SpawnTransforms": use those, the same way the controller would.
    private void CollectFallbackLanes(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "SpawnTransforms") continue;

                foreach (Transform child in t)
                {
                    if (child != null && !lanes.Contains(child)) lanes.Add(child);
                }
            }
        }

        if (lanes.Count > 0 && logActions)
            InteractionDebug.Log($"[MP-CAR] no spawn points on the car controller: using {lanes.Count} marker(s) under 'SpawnTransforms'.");
    }

    private void CollectLanes(Scene scene)
    {
        if (!scene.IsValid() || SpawnPointsField == null) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CarController_Prototype controller in
                     root.GetComponentsInChildren<CarController_Prototype>(true))
            {
                if (!(SpawnPointsField.GetValue(controller) is Transform[] points)) continue;

                foreach (Transform point in points)
                {
                    if (point == null || lanes.Contains(point)) continue;
                    lanes.Add(point);
                }
            }
        }

        if (lanes.Count == 0) CollectFallbackLanes(scene);

        SeparateOppositeLanes();

        // The lane markers are raised lazily, on the first spawn: at bind time the scene is not fully there yet and the
        // road lookup came back empty, which also poisoned the cache the seating uses.
        lanesAligned = false;
    }

    private void SeparateOppositeLanes()
    {
        laneShift.Clear();

        // Level 4 was authored with its two E-W spawn points on the same centre line (0.07 m apart): the level has one
        // line of traffic there. Pulling them 2.8 m apart invented a second line the level does not have.
        if (boundScene.IsValid() && boundScene.name == "Level_04")
        {
            // One line of traffic means cars from ONE direction: of two opposite spawn points on that line, only the
            // first is used (both used to send cars head-on along the same lane).
            for (int i = lanes.Count - 1; i >= 0; i--)
            {
                Transform a = lanes[i];
                if (a == null) continue;

                Vector3 forward = a.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = new Vector3(forward.z, 0f, -forward.x);

                for (int j = 0; j < i; j++)
                {
                    Transform b = lanes[j];
                    if (b == null) continue;

                    Vector3 other = b.forward; other.y = 0f; other.Normalize();
                    if (Vector3.Dot(other, forward) > -0.9f) continue; // not the opposite direction

                    Vector3 delta = b.position - a.position; delta.y = 0f;
                    if (Mathf.Abs(Vector3.Dot(delta, right)) > 1.5f) continue; // a different line

                    lanes.RemoveAt(i);
                    if (logActions) InteractionDebug.Log($"[MP-CAR] Level_04: lane '{a.name}' dropped (opposite direction on the same line as '{b.name}').");
                    break;
                }
            }

            if (logActions)
                InteractionDebug.Log($"[MP-CAR] Level_04 keeps its single centre line, one direction ({lanes.Count} lane(s)).");
            return;
        }

        foreach (Transform a in lanes)
        {
            if (a == null) continue;

            Vector3 forward = a.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float shift = 0f;

            foreach (Transform b in lanes)
            {
                if (b == null || b == a) continue;

                Vector3 other = b.forward; other.y = 0f; other.Normalize();
                if (Vector3.Dot(other, forward) > -0.9f) continue; // not the opposite direction

                Vector3 delta = b.position - a.position; delta.y = 0f;
                float along = Vector3.Dot(delta, forward);
                float side = Vector3.Dot(delta, right); // where the opposite lane lies, seen from this lane
                if (Mathf.Abs(along) < 20f || Mathf.Abs(side) > 12f) continue; // not the same road

                // Ideal: the opposite lane is OppositeLaneGap to our left (side = -gap); both lanes move half the missing way.
                shift = Mathf.Max(shift, (side + OppositeLaneGap) * 0.5f);
            }

            if (shift > 0.05f) laneShift[a] = right * shift;
        }

        if (laneShift.Count > 0 && logActions)
            InteractionDebug.Log($"[MP-CAR] {laneShift.Count} lane(s) moved sideways so opposite directions are {OppositeLaneGap:0.0} m apart.");
    }

    private Vector3 ShiftOf(Transform lane) => laneShift.TryGetValue(lane, out Vector3 shift) ? shift : Vector3.zero;

    private GameObject SpawnOne(GameObject prefab, bool ignoreLimit = false)
    {
        if (prefab == null) return null;

        PruneLiveCars();

        if (!ignoreLimit && liveCars.Count >= MaxCars) return null;

        if (!lanesAligned)
        {
            lanesAligned = true;
            AlignLanesToRoad();
        }

        Transform lane = PickFreeLane();
        if (lane == null) return null;

        Vector3 shift = ShiftOf(lane);
        GameObject car = Instantiate(prefab, lane.position + shift, lane.rotation);
        SeatOnRoad(car, lane);

        // Road start = this lane's spawn point, road end = where the road leaves the map (found from the lane on the
        // opposite side). The car is removed when it passes the end instead of after a fixed time mid-road.
        if (TryFindRoadEnd(lane, out Vector3 roadEnd) && car.TryGetComponent(out MpCarHitSync_MP route))
            route.SetRoute(roadEnd + shift, lane.forward);

        NetworkServer.Spawn(car);

        liveCars.Add(car);
        spawnedTotal++;

        if (logActions)
            InteractionDebug.Log($"[MP-CAR] server spawn '{car.name}' lane='{lane.name}' " +
                                 $"netId={car.GetComponent<NetworkIdentity>()?.netId} (total {spawnedTotal})");

        return car;
    }

    // The road meshes have no collider and sit a little above the ground collider, while the lane transforms were placed
    // for the ground and at a different height in every level: wheels ended up inside the asphalt (Level 3) or floating
    // (Level 1). So a spawned car is put on the road MESH found under its lane: its lowest mesh point = the road top.
    private const float WheelClearance = 0.015f;
    private MeshRenderer[] roadSlabs;
    private bool lanesAligned;

    // Road top under a point, seen from a reference height (the lane marker or the car).
    // The highest slab at or just above the reference wins, so a water decal lying on the asphalt does not count as the
    // road; anything further above than 0.6 m is treated as a roof/canopy and ignored. If nothing is in that band the
    // highest slab within +/-3 m is used, which covers level markers that sit on the ground instead of on the road.
    private bool TryRoadTop(Vector3 point, float referenceY, out float top)
    {
        top = float.MinValue;

        float insideTop = float.MinValue;
        float nearTop = float.MinValue;
        float nearDistance = 15f;

        foreach (MeshRenderer slab in roadSlabs)
        {
            if (slab == null) continue;

            Bounds b = slab.bounds;

            float delta = b.max.y - referenceY;
            if (delta < -3f || delta > 3f) continue;      // another floor / a roof

            bool usable = delta <= 0.6f;                  // at the reference or just above it

            float dx = Mathf.Max(b.min.x - point.x, 0f, point.x - b.max.x);
            float dz = Mathf.Max(b.min.z - point.z, 0f, point.z - b.max.z);
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            if (distance <= 0.001f)
            {
                // Straight under the point: the highest usable slab wins (a water decal on the asphalt is lower).
                if (usable && (insideTop == float.MinValue || delta > insideTop - referenceY)) insideTop = b.max.y;
                continue;
            }

            // Level 5 and the versus scene put their lane markers past the end of the road mesh, so nothing is straight
            // under them. Fall back to the nearest road surface around the point instead of leaving the car in the air.
            if (distance < nearDistance)
            {
                nearDistance = distance;
                nearTop = b.max.y;
            }
        }

        top = insideTop != float.MinValue ? insideTop : nearTop;
        return top != float.MinValue;
    }

    // The lane markers are also the SP car controller's waypoints. A marker sitting on the ground (Level 5, versus)
    // pulled every car back down to y = 0 after the spawn had been corrected, so the markers are raised to the road
    // themselves once per scene; the controller then drives on the road and the spawn continues to match it.
    private void AlignLanesToRoad()
    {
        if (roadSlabs == null || roadSlabs.Length == 0) roadSlabs = FindRoadSlabs();

        int moved = 0;

        foreach (Transform lane in lanes)
        {
            if (lane == null) continue;

            Vector3 p = lane.position;
            if (!TryRoadTop(p, p.y, out float top)) continue;
            if (Mathf.Abs(top - p.y) < 0.01f) continue;

            lane.position = new Vector3(p.x, top, p.z);
            moved++;

            if (logActions)
                InteractionDebug.Log($"[MP-CAR] lane '{lane.name}' raised {p.y:0.00} -> {top:0.00} (road top).");
        }

        if (moved > 0 && logActions)
            InteractionDebug.Log($"[MP-CAR] {moved} lane marker(s) aligned to the road surface.");
    }

    private void SeatOnRoad(GameObject car, Transform lane)
    {
        if (roadSlabs == null || roadSlabs.Length == 0) roadSlabs = FindRoadSlabs();

        if (!TryRoadTop(car.transform.position, lane.position.y, out float roadTop)) return;

        float bottom = float.MaxValue;
        foreach (Renderer r in car.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            bottom = Mathf.Min(bottom, r.bounds.min.y);
        }

        if (bottom == float.MaxValue) return;

        car.transform.position += Vector3.up * (roadTop + WheelClearance - bottom);
    }

    // Flat, wide mesh slabs of the level: road, pavement, ground tiles.
    private static MeshRenderer[] FindRoadSlabs()
    {
        var found = new System.Collections.Generic.List<MeshRenderer>();

        foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            Bounds b = r.bounds;
            if (b.size.y > 1.5f || b.size.x < 3f || b.size.z < 3f) continue;
            if (r.GetComponentInParent<NetworkIdentity>() != null) continue;

            found.Add(r);
        }

        return found.ToArray();
    }

    // The lane facing the opposite way on (almost) the same line marks the far end of the road.
    private bool TryFindRoadEnd(Transform lane, out Vector3 end)
    {
        end = default;
        float bestLateral = float.MaxValue;

        foreach (Transform other in lanes)
        {
            if (other == null || other == lane) continue;
            if (Vector3.Dot(other.forward, lane.forward) > -0.9f) continue;

            Vector3 delta = other.position - lane.position;
            float along = Vector3.Dot(delta, lane.forward);
            if (along < 20f) continue;

            float lateral = (delta - lane.forward * along).magnitude;
            if (lateral > 12f || lateral >= bestLateral) continue;

            bestLateral = lateral;
            end = lane.position + lane.forward * along;
        }

        return bestLateral < float.MaxValue;
    }

    // Versus: at most 2 cars at a time, one per lane (the two-lane road had four cars on it). Co-op keeps the inspector value.
    private int MaxCars => boundScene.IsValid() && boundScene.name.IndexOf("Versus", System.StringComparison.OrdinalIgnoreCase) >= 0
        ? Mathf.Max(1, Mathf.Min(maxConcurrent, 2))
        : Mathf.Max(1, maxConcurrent);

    private Transform PickFreeLane()
    {
        int start = Random.Range(0, lanes.Count);
        float clearSqr = laneClearRadius * laneClearRadius;

        for (int offset = 0; offset < lanes.Count; offset++)
        {
            Transform lane = lanes[(start + offset) % lanes.Count];
            if (lane == null) continue;

            bool occupied = false;

            foreach (GameObject car in liveCars)
            {
                if (car == null) continue;
                if ((car.transform.position - lane.position).sqrMagnitude >= clearSqr) continue;

                occupied = true;
                break;
            }

            if (!occupied) return lane;
        }

        return null;
    }

    private void PruneLiveCars() => liveCars.RemoveAll(car => car == null);

    private GameObject ResolvePrefab()
    {
        if (carPrefab != null) return RegisterIfNeeded(carPrefab);

        LobbyNetworkManager manager = NetworkManager.singleton as LobbyNetworkManager;
        if (manager != null && manager.TrafficCarPrefab != null)
            return RegisterIfNeeded(manager.TrafficCarPrefab);

        if (resourcesPrefab == null) resourcesPrefab = Resources.Load<GameObject>(ResourceName);
        if (resourcesPrefab != null) return RegisterIfNeeded(resourcesPrefab);

        if (!prefabWarningLogged)
        {
            prefabWarningLogged = true;
            InteractionDebug.Log($"[MP-CAR] no networked car prefab - '{ResourceName}' missing from " +
                                 "Assets/Prefabs_MP/Cars/Resources and LobbyNetworkManager.trafficCarPrefab is empty.");
        }

        return null;
    }

    private static GameObject RegisterIfNeeded(GameObject prefab)
    {
        if (prefab == null) return null;

        NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();

        if (identity == null)
        {
            InteractionDebug.Log($"[MP-CAR] '{prefab.name}' has no NetworkIdentity - it cannot be spawned.");
            return null;
        }

        if (!NetworkClient.GetPrefab(identity.assetId, out _))
            NetworkClient.RegisterPrefab(prefab);

        return prefab;
    }

    private string DescribePrefab()
    {
        GameObject prefab = ResolvePrefab();
        return prefab != null ? prefab.name : "MISSING";
    }
}
