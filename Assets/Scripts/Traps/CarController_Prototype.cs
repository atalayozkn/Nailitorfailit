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

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    private List<GameObject> activeCarPool = new();
    private float selectedInterval;
    private void Start()
    {
        CreatePool(activeCarPrefab, activePoolSize, activeCarPool);

        selectedInterval = activeCarInterval;
        StartCoroutine(CarRoutine());
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
        GameObject car = GetAvailableCar(activeCarPool);
        if (car == null) return;

        SendCar(car);
    }
    private void SendCar(GameObject car)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        car.transform.SetPositionAndRotation(
            spawnPoint.position,
            spawnPoint.rotation
        );

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