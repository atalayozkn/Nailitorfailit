using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController_Prototype : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject activeCarPrefab;
    [Tooltip("Extra car models mixed into the traffic. Each needs a Car_Active component.")]
    [SerializeField] private GameObject[] extraCarPrefabs;

    [Header("Pool Settings")]
    [Tooltip("Total pooled cars. Raised automatically so every car model gets at least one.")]
    [SerializeField] private int activePoolSize = 4;

    [Header("Traffic Settings")]
    [SerializeField] private float activeCarInterval = 2f;
    [Tooltip("0 = lane stays occupied until the car is despawned. " +
             "Above 0 = lane is freed once the car has been alive this many seconds.")]
    [SerializeField] private float laneClearTime = 0f;

    [Header("Oil Truck Hazard (singleplayer only)")]
    [Tooltip("Leave empty to turn the hazard off. Needs Car_Active and OilTruckSpiller.")]
    [SerializeField] private GameObject oilTruckPrefab;
    [Tooltip("Seconds between oil trucks (random in range). Also used for the first one.")]
    [SerializeField] private Vector2 oilTruckInterval = new(25f, 40f);
    [Tooltip("Seconds before the first oil truck. Below 0 = use the interval.")]
    [SerializeField] private float oilTruckFirstDelay = -1f;
    [SerializeField] private float oilTruckRetryDelay = 0.5f;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Ground Snap")]
    [Tooltip("Puts each car's collider bottom on the road when it spawns, so it never starts inside the ground.")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float snapProbeUp = 2f;
    [SerializeField] private float snapProbeDistance = 6f;
    [SerializeField] private float groundClearance = 0.03f;

    private readonly List<Car_Active> activeCarPool = new();
    private readonly Dictionary<Car_Active, float> carBottomOffsets = new();
    private Car_Active[] laneOccupants;
    private readonly List<int> freeLanes = new();
    private readonly List<Car_Active> freeCars = new();
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private Car_Active oilTruck;

    private void Start()
    {
        laneOccupants = new Car_Active[spawnPoints.Length];

        CreatePool(activeCarPool);
        StartCoroutine(CarRoutine());

        if (CreateOilTruck()) StartCoroutine(OilTruckRoutine());
    }

    private bool CreateOilTruck()
    {
        if (oilTruckPrefab == null || NetworkModeHelper.IsNetworked) return false;

        GameObject obj = Instantiate(oilTruckPrefab, transform);
        obj.SetActive(false);

        if (!obj.TryGetComponent(out oilTruck))
        {
            Debug.LogError("Oil truck prefab is missing the Car_Active component.", oilTruckPrefab);
            Destroy(obj);
            return false;
        }

        carBottomOffsets[oilTruck] = GetColliderBottom(obj);
        return true;
    }

    private IEnumerator OilTruckRoutine()
    {
        var retry = new WaitForSeconds(oilTruckRetryDelay);
        bool first = true;

        while (true)
        {
            float delay = first && oilTruckFirstDelay >= 0f
                ? oilTruckFirstDelay
                : Random.Range(oilTruckInterval.x, oilTruckInterval.y);
            first = false;
            yield return new WaitForSeconds(delay);

            // Wait for the previous truck to leave and for a lane with no car in it.
            int lane = -1;
            while (oilTruck.gameObject.activeSelf || (lane = GetRandomFreeLane()) < 0)
                yield return retry;

            SendCar(oilTruck, lane);
        }
    }

    private IEnumerator CarRoutine()
    {
        var wait = new WaitForSeconds(activeCarInterval);

        while (true)
        {
            yield return wait;
            SendCar();
        }
    }

    private void SendCar()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        int lane = GetRandomFreeLane();
        if (lane < 0) return; // every lane is busy, try again next interval

        Car_Active car = GetAvailableCar(activeCarPool);
        if (car == null) return; // pool exhausted

        SendCar(car, lane);
    }

    private void SendCar(Car_Active car, int lane)
    {
        // The car may still be registered to its previous lane
        for (int i = 0; i < laneOccupants.Length; i++)
        {
            if (laneOccupants[i] == car) laneOccupants[i] = null;
        }

        Transform spawnPoint = spawnPoints[lane];
        Vector3 position = spawnPoint.position;
        if (snapToGround) position = SnapToGround(car, position);

        car.transform.SetPositionAndRotation(position, spawnPoint.rotation);

        laneOccupants[lane] = car;
        car.gameObject.SetActive(true);
    }

    private Vector3 SnapToGround(Car_Active car, Vector3 position)
    {
        Vector3 origin = position + Vector3.up * snapProbeUp;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, snapProbeDistance, groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        bool found = false;
        Vector3 ground = position;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (!IsGround(hit.collider)) continue;
            if (hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            ground = hit.point;
            found = true;
        }

        if (!found) return position;

        carBottomOffsets.TryGetValue(car, out float bottom);
        position.y = ground.y - bottom + groundClearance;
        return position;
    }

    // Moving bodies (other cars, the player, props) are not road.
    private static bool IsGround(Collider col)
    {
        if (col.CompareTag("Player")) return false;

        Rigidbody body = col.attachedRigidbody;
        if (body == null) return true;
        if (body.GetComponent<Car_Active>() != null) return false;
        return body.isKinematic;
    }

    private int GetRandomFreeLane()
    {
        freeLanes.Clear();

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (IsLaneFree(i)) freeLanes.Add(i);
        }

        if (freeLanes.Count == 0) return -1;
        return freeLanes[Random.Range(0, freeLanes.Count)];
    }

    private bool IsLaneFree(int lane)
    {
        Car_Active occupant = laneOccupants[lane];

        if (occupant == null) return true;
        if (!occupant.gameObject.activeSelf) return true;

        if (laneClearTime > 0f && occupant.GetElapsedLifeTime() >= laneClearTime)
            return true;

        return false;
    }

    // Random free car so the models mix.
    private Car_Active GetAvailableCar(List<Car_Active> pool)
    {
        freeCars.Clear();

        foreach (Car_Active car in pool)
        {
            if (!car.gameObject.activeSelf) freeCars.Add(car);
        }

        if (freeCars.Count == 0) return null;
        return freeCars[Random.Range(0, freeCars.Count)];
    }

    private void CreatePool(List<Car_Active> pool)
    {
        List<GameObject> prefabs = new();
        if (activeCarPrefab != null) prefabs.Add(activeCarPrefab);

        // Extra models are not networked, multiplayer keeps its single swapped car.
        if (extraCarPrefabs != null && !NetworkModeHelper.IsNetworked)
        {
            foreach (GameObject extra in extraCarPrefabs)
            {
                if (extra != null && !prefabs.Contains(extra)) prefabs.Add(extra);
            }
        }

        if (prefabs.Count == 0) return;

        int poolSize = Mathf.Max(activePoolSize, prefabs.Count);

        for (int i = 0; i < poolSize; i++)
        {
            GameObject prefab = prefabs[i % prefabs.Count];
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);

            if (!obj.TryGetComponent(out Car_Active car))
            {
                Debug.LogError("Car prefab is missing the Car_Active component.", prefab);
                Destroy(obj);
                continue;
            }

            carBottomOffsets[car] = GetColliderBottom(obj);
            pool.Add(car);
        }
    }

    // Lowest point of the car's solid colliders, measured from its pivot.
    private static float GetColliderBottom(GameObject car)
    {
        float bottom = float.MaxValue;

        foreach (BoxCollider box in car.GetComponentsInChildren<BoxCollider>(true))
        {
            if (box.isTrigger) continue;

            Transform t = box.transform;
            Vector3 localBottom = box.center - Vector3.up * (box.size.y * 0.5f);
            Vector3 world = t.TransformPoint(localBottom);
            float y = car.transform.InverseTransformPoint(world).y * car.transform.lossyScale.y;
            bottom = Mathf.Min(bottom, y);
        }

        return bottom == float.MaxValue ? 0f : bottom;
    }
}
