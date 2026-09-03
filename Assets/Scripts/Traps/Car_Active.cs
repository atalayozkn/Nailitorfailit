using UnityEngine;
using UnityEngine.Events;

public class Car_Active : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private GameObject mockPlayerObject;
    [SerializeField] private Rigidbody rb;

    [Header("Settings")]
    [SerializeField] private float maxLifeTime;
    [SerializeField] private float maxMoveForce;
    [SerializeField] private float minMoveForce;

    [Header("Events")]
    [SerializeField] private UnityEvent onCrashEvent;

    private float movementForce;
    private float counter;

    private void OnEnable()
    {
        counter = 0f;
        movementForce = Random.Range(minMoveForce, maxMoveForce);
        mockPlayerObject.SetActive(false);
        rb.WakeUp();
    }

    private void OnDisable()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
    }

    private void FixedUpdate()
    {
        rb.AddForce(transform.forward * movementForce);
    }

    private void Update()
    {
        counter += Time.deltaTime;

        if (counter >= maxLifeTime)
        {
            gameObject.SetActive(false);
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if ((whatIsPlayer.value & (1 << collision.gameObject.layer)) == 0) return;
        Bump(collision.gameObject);
    }
    private void Bump(GameObject obj)
    {
        if (obj.TryGetComponent<PlayerStateMachine>(out var stateMachine))
        {
            stateMachine.ChangeToStunnedState();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if ((whatIsPlayer.value & (1 << other.gameObject.layer)) == 0) return;
        Crash(other);
    }
    private void Crash(Collider other)
    {
        if (other.gameObject.TryGetComponent<PlayerStateMachine>(out var stateMachine))
        {
            if (stateMachine.isDead) return;
            stateMachine.SetTrackTarget(transform);
            stateMachine.SetDeathReason(DeathReason.Car);
            stateMachine.ChangeToDeadState();
            mockPlayerObject.SetActive(true);
            onCrashEvent?.Invoke();
        }
    }
}