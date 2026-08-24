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
    [SerializeField] private bool canCrash;
    [SerializeField] private float maxMoveForce;
    [SerializeField] private float minMoveForce;

    private float movementForce;
    private float counter;

    private void OnEnable()
    {
        counter = 0f;
        movementForce = Random.Range(minMoveForce, maxMoveForce);
        mockPlayerObject.SetActive(false);
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

    private void OnTriggerEnter(Collider other)
    {
        if (!canCrash) return;
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
        }
    }
}