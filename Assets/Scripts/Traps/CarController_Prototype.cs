using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController_Prototype : MonoBehaviour
{
    private enum TrafficMode
    {
        Passive,
        Active
    }

    [Header("References")]
    [SerializeField] private LayerMask whatIsPlayer;

    [SerializeField] private GameObject activeCarPrefab;
    [SerializeField] private GameObject passiveCarPrefab;

    [Header("Pool Settings")]
    [SerializeField] private int activePoolSize = 4;
    [SerializeField] private int passivePoolSize = 4;

    [Header("Traffic Settings")]
    [SerializeField] private float activeCarInterval = 2f;
    [SerializeField] private float passiveCarInterval = 5f;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    private List<GameObject> activeCarPool = new();
    private List<GameObject> passiveCarPool = new();

    private TrafficMode currentMode = TrafficMode.Passive;

    private float selectedInterval;
    private bool isPlayerInside;
    private Coroutine carRoutine;

    private void Start()
    {
        CreatePool(activeCarPrefab, activePoolSize, activeCarPool);
        CreatePool(passiveCarPrefab, passivePoolSize, passiveCarPool);

        ChangePhase(TrafficMode.Passive);
        carRoutine = StartCoroutine(CarRoutine());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPlayerInside) return;
        if ((whatIsPlayer.value & (1 << other.gameObject.layer)) == 0) return;

        isPlayerInside = true;
        ChangePhase(TrafficMode.Active);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isPlayerInside) return;
        if ((whatIsPlayer.value & (1 << other.gameObject.layer)) == 0) return;

        isPlayerInside = false;
        ChangePhase(TrafficMode.Passive);
    }

    private void ChangePhase(TrafficMode newMode)
    {
        currentMode = newMode;

        switch (currentMode)
        {
            case TrafficMode.Active:
                selectedInterval = activeCarInterval;
                StopCoroutine(carRoutine);
                carRoutine = StartCoroutine(CarRoutine());
                break;

            case TrafficMode.Passive:
                selectedInterval = passiveCarInterval;
                break;
        }
    }

    private IEnumerator CarRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(selectedInterval);
            SendCar();
        }
    }

    private void SendCar()
    {
        switch (currentMode)
        {
            case TrafficMode.Active:
                SendActiveCar();
                break;

            case TrafficMode.Passive:
                SendPassiveCar();
                break;
        }
    }

    private void SendActiveCar()
    {
        GameObject car = GetAvailableCar(activeCarPool);
        if (car == null) return;
        SendCar(car);
    }

    private void SendPassiveCar()
    {
        GameObject car = GetAvailableCar(passiveCarPool);
        if (car == null) return;
        SendCar(car);
    }

    private void SendCar(GameObject car)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        car.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        car.SetActive(true);
    }

    private GameObject GetAvailableCar(List<GameObject> pool)
    {
        foreach (GameObject car in pool)
        {
            if (!car.activeSelf) return car;
        }

        return null;
    }

    private void CreatePool(GameObject prefab, int poolSize, List<GameObject> pool)
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject car = Instantiate(prefab, transform);
            car.SetActive(false);
            pool.Add(car);
        }
    }
}