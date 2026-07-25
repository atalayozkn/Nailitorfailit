using UnityEngine;
using UnityEngine.Events;

public class TrapVehicle : MonoBehaviour
{
    [Header("Respawn Manager")]
    [SerializeField] private PlayerTrapRespawn playerTrapRespawn;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Vehicle Movement")]
    [SerializeField] private Rigidbody vehicleRigidbody;
    [SerializeField] private Vector3 vehicleMoveDirection = Vector3.right;
    [SerializeField] private float vehicleMoveSpeed = 15f;

    [Header("Vehicle Disable")]
    [SerializeField] private bool disableVehicleAfterTravel = true;
    [SerializeField] private float vehicleDisableAfterSeconds = 8f;

    [Header("Vehicle Player Object")]
    [SerializeField] private GameObject vehiclePlayerObject;
    [SerializeField] private bool disableVehiclePlayerObjectOnStart = true;

    [Header("Respawn")]
    [SerializeField] private float vehicleRespawnDelay = 3f;

    [Header("Events")]
    [SerializeField] private UnityEvent onTrapTriggeredEvent;
    [SerializeField] private UnityEvent onVehicleTrapEvent;

    private bool hasHitPlayer;
    private float vehicleDisableCounter;

    private void Awake()
    {
        ResolveRespawnManager();

        if (vehicleRigidbody == null)
            vehicleRigidbody = GetComponent<Rigidbody>();

        SetVehiclePlayerObject(false);
    }

    private void OnEnable()
    {
        ResolveRespawnManager();

        hasHitPlayer = false;
        vehicleDisableCounter = 0f;

        if (disableVehiclePlayerObjectOnStart)
            SetVehiclePlayerObject(false);

        ResetVehiclePhysics();
    }

    private void OnDisable()
    {
        ResetVehiclePhysics();

        if (disableVehiclePlayerObjectOnStart)
            SetVehiclePlayerObject(false);

        hasHitPlayer = false;
        vehicleDisableCounter = 0f;
    }

    private void Update()
    {
        HandleVehicleDisableCounter();
    }

    private void FixedUpdate()
    {
        MoveVehicle();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitPlayer(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHitPlayer(collision.collider);
    }

    private void TryHitPlayer(Collider other)
    {
        if (hasHitPlayer)
            return;

        if (!other.CompareTag(playerTag))
            return;

        ResolveRespawnManager();

        if (playerTrapRespawn == null)
        {
            Debug.LogWarning("PlayerTrapRespawn bulunamadý!");
            return;
        }

        hasHitPlayer = true;

        onTrapTriggeredEvent?.Invoke();
        onVehicleTrapEvent?.Invoke();

        SetVehiclePlayerObject(true);

        playerTrapRespawn.RespawnPlayer(vehicleRespawnDelay);
    }

    private void MoveVehicle()
    {
        if (vehicleRigidbody == null)
            return;

        Vector3 direction = vehicleMoveDirection.normalized;

        vehicleRigidbody.linearVelocity = direction * vehicleMoveSpeed;
        vehicleRigidbody.angularVelocity = Vector3.zero;
    }

    private void HandleVehicleDisableCounter()
    {
        if (!disableVehicleAfterTravel)
            return;

        vehicleDisableCounter += Time.deltaTime;

        if (vehicleDisableCounter >= vehicleDisableAfterSeconds)
            gameObject.SetActive(false);
    }

    private void ResetVehiclePhysics()
    {
        if (vehicleRigidbody == null)
            return;

        vehicleRigidbody.linearVelocity = Vector3.zero;
        vehicleRigidbody.angularVelocity = Vector3.zero;
    }

    private void SetVehiclePlayerObject(bool condition)
    {
        if (vehiclePlayerObject == null)
            return;

        vehiclePlayerObject.SetActive(condition);
    }

    private void ResolveRespawnManager()
    {
        if (playerTrapRespawn != null)
            return;

        if (PlayerTrapRespawn.Instance != null)
            playerTrapRespawn = PlayerTrapRespawn.Instance;
        else
            playerTrapRespawn = FindFirstObjectByType<PlayerTrapRespawn>();
    }
}