using System.Collections;
using UnityEngine;

public class VehicleTrapCaller : MonoBehaviour
{
    [Header("Vehicle Object")]
    [SerializeField] private GameObject vehicleObject;

    [Header("Start Point")]
    [SerializeField] private Transform vehicleStartPoint;

    [Header("Random Call Time")]
    [SerializeField] private float minCallDelay = 1f;
    [SerializeField] private float maxCallDelay = 4f;

    [Header("Trigger Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool callOnlyOncePerEnter = true;
    [SerializeField] private bool repeatWhilePlayerInside = false;
    [SerializeField] private float repeatDelayAfterCall = 3f;

    [Header("Counter")]
    private float counter = 0f;
    private float randomizedDelay;
    [SerializeField] private float minSpawnDelay;
    [SerializeField] private float maxSpawnDelay;

    private bool isPlayerInside;
    private bool hasCalledVehicle;

    private void Awake()
    {
        ResetVehicle();
        randomizedDelay = RollDice();
    }

    private void Update()
    {
        if(!isPlayerInside)
        {
            return;
        }

        counter += Time.deltaTime;

        if(counter >= randomizedDelay)
        {
            counter = 0f;
            randomizedDelay = RollDice();
            CallVehicleRoutine();
        }
    }

    private float RollDice()
    {
        float random = UnityEngine.Random.Range(minSpawnDelay, maxSpawnDelay);
        return random;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        isPlayerInside = true;

        if (callOnlyOncePerEnter && hasCalledVehicle)
            return;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        isPlayerInside = false;
        hasCalledVehicle = false;
    }

    private void CallVehicleRoutine()
    {
        float randomDelay = Random.Range(minCallDelay, maxCallDelay);

        if (!isPlayerInside)
            return;

        ActivateVehicle();

        hasCalledVehicle = true;

        if (!repeatWhilePlayerInside)
            return;
    }

    private void ActivateVehicle()
    {
        if (vehicleObject == null)
        {
            Debug.LogWarning("Vehicle Object atanmadý!");
            return;
        }

        vehicleObject.transform.position = vehicleStartPoint.position;
        vehicleObject.transform.rotation = vehicleStartPoint.rotation;
        vehicleObject.SetActive(true);
    }

    private void ResetVehicle()
    {
        if (vehicleObject == null)
            return;

        if (vehicleObject == null)
            return;

        if (vehicleStartPoint == null)
        {
            vehicleObject.transform.position = transform.position;
            vehicleObject.transform.rotation = transform.rotation;
            return;
        }

        vehicleObject.transform.position = vehicleStartPoint.position;
        vehicleObject.transform.rotation = vehicleStartPoint.rotation;

        vehicleObject.SetActive(false);
    }
}