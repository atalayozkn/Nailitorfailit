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

    private static readonly FieldInfo SpawnPointsField =
        typeof(CarController_Prototype).GetField("spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic);

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

    private void Update()
    {
        if (!NetworkServer.active && !NetworkClient.active) return;

        Scene active = SceneManager.GetActiveScene();
        if (active != boundScene) BindScene(active);

        if (!NetworkServer.active || lanes.Count == 0) return;
        if (Time.unscaledTime < nextSpawnAt) return;

        nextSpawnAt = Time.unscaledTime + Mathf.Max(0.5f, interval);
        SpawnOne();
    }

    private void BindScene(Scene scene)
    {
        boundScene = scene;
        lanes.Clear();
        liveCars.Clear();

        DisableAuthoredTraffic(scene);
        CollectLanes(scene);

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
                    if (!controller.enabled) continue;

                    controller.enabled = false;

                    if (logActions)
                        InteractionDebug.Log($"[MP-CAR] local traffic '{controller.name}' disabled " +
                                             "(the server spawns the networked cars).");
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
    }

    private void SpawnOne()
    {
        GameObject prefab = ResolvePrefab();
        if (prefab == null) return;

        PruneLiveCars();

        Transform lane = PickFreeLane();
        if (lane == null) return;

        GameObject car = Instantiate(prefab, lane.position, lane.rotation);
        NetworkServer.Spawn(car);

        liveCars.Add(car);
        spawnedTotal++;

        if (logActions)
            InteractionDebug.Log($"[MP-CAR] server spawn '{car.name}' lane='{lane.name}' " +
                                 $"netId={car.GetComponent<NetworkIdentity>()?.netId} (total {spawnedTotal})");
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
