using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RunDustEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem runDust;

    [Header("Tuning")]
    [SerializeField] private float minSpeedToEmit = 2.2f;
    [SerializeField] private float stopDelay = 0.08f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.18f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody rb;
    private float stopTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (runDust != null) runDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
        if (runDust == null) return;

        bool grounded = IsGrounded();
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

        bool shouldEmit = grounded && horizontalSpeed >= minSpeedToEmit;

        if (shouldEmit)
        {
            stopTimer = 0f;
            if (!runDust.isPlaying) runDust.Play();
        }
        else
        {
            stopTimer += Time.deltaTime;
            if (runDust.isPlaying && stopTimer >= stopDelay)
                runDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private bool IsGrounded()
    {
        if (groundCheck == null) return true; // groundCheck yoksa sadece hýza bakar
        return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}