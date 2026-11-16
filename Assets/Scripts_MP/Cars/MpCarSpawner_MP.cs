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
    }

    private void SeparateOppositeLanes()
    {
        laneShift.Clear();

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

        if (!ignoreLimit && liveCars.Count >= Mathf.Max(1, maxConcurrent)) return null;

        Transform lane = PickFreeLane();
        if (lane == null) return null;

        Vector3 shift = ShiftOf(lane);
        GameObject car = Instantiate(prefab, lane.position + shift, lane.rotation);

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
