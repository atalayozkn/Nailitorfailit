using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController_Prototype : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject activeCarPrefab;

    [Header("Pool Settings")]
    [SerializeField] private int activePoolSize = 4;

    [Header("Traffic Settings")]
    [SerializeField] private float activeCarInterval = 2f;
    [Tooltip("0 = lane stays occupied until the car is despawned. " +
             "Above 0 = lane is freed once the car has been alive this many seconds.")]
    [SerializeField] private float laneClearTime = 0f;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    private readonly List<Car_Active> activeCarPool = new();
    private Car_Active[] laneOccupants;
    private readonly List<int> freeLanes = new();

    private void Start()
    {
        laneOccupants = new Car_Active[spawnPoints.Length];

        CreatePool(activeCarPrefab, activePoolSize, activeCarPool);
        StartCoroutine(CarRoutine());
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
        car.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        laneOccupants[lane] = car;
        car.gameObject.SetActive(true);
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

    private Car_Active GetAvailableCar(List<Car_Active> pool)
    {
        foreach (Car_Active car in pool)
        {
            if (!car.gameObject.activeSelf) return car;
        }

        return null;
    }

    private void CreatePool(GameObject prefab, int poolSize, List<Car_Active> pool)
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);

            if (!obj.TryGetComponent(out Car_Active car))
            {
                Debug.LogError("Car prefab is missing the Car_Active component.", prefab);
                Destroy(obj);
                continue;
            }

            pool.Add(car);
        }
    }
}