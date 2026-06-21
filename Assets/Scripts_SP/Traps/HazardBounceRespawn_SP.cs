using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerScripts;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMove_SP))]
public class HazardBounceRespawn_SP : MonoBehaviour
{
    [Header("Trap")]
    [SerializeField] private string trapTag = "Trap";

    [Header("Visual")]
    [SerializeField] private GameObject visualRoot;

    [Header("Ground Check (Layer)")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckExtra = 0.05f;
    [SerializeField] private Collider playerCollider;

    [Header("Disable inputs until respawn")]
    [SerializeField] private InputActionReference move;
    [SerializeField] private InputActionReference jump;
    [SerializeField] private InputActionReference sprint;

    [Header("Anti Double Trigger")]
    [SerializeField] private float hitCooldown = 0.05f;

    [Header("Performance")]
    [SerializeField] private float groundCheckInterval = 0.03f;

    private Rigidbody rb;
    private PlayerMove_SP playerMove;
    private float lastHitTime = -999f;
    private Coroutine trapRoutine;

    public bool IsTrapped { get; private set; }
    public bool IsInvisible { get; private set; }
    public float InvisibleStartTime { get; private set; } = -1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerMove = GetComponent<PlayerMove_SP>();

        if (playerCollider == null)
            playerCollider = GetComponent<Collider>();

        if (move == null) move = playerMove.move;
        if (jump == null) jump = playerMove.jump;
        if (sprint == null) sprint = playerMove.sprint;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTrap(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryTrap(collision.collider);
    }

    private void TryTrap(Collider other)
    {
        if (IsInvisible) return;
        if (trapRoutine != null) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        if (!other.CompareTag(trapTag)) return;

        TrapHazard trap = other.GetComponentInParent<TrapHazard>();
        if (trap == null) return;

        lastHitTime = Time.time;

        Vector3 v = rb.linearVelocity;
        v.y = trap.BounceUpForce;
        rb.linearVelocity = v;

        LockInputs();

        trapRoutine = StartCoroutine(TrapRoutine());
    }

    private IEnumerator TrapRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(groundCheckInterval);

        while (IsGroundedByCollider())
        {
            yield return wait;
        }

        while (!IsGroundedByCollider())
        {
            yield return wait;
        }

        MakeInvisibleAndStartCountdown();

        trapRoutine = null;
    }

    private void MakeInvisibleAndStartCountdown()
    {
        IsInvisible = true;
        IsTrapped = true;
        InvisibleStartTime = Time.time;

        if (visualRoot != null)
            visualRoot.SetActive(false);
    }

    private bool IsGroundedByCollider()
    {
        if (playerCollider == null) return false;

        Vector3 origin = playerCollider.bounds.center;
        float distance = playerCollider.bounds.extents.y + groundCheckExtra;

        return Physics.Raycast(
            origin,
            Vector3.down,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void LockInputs()
    {
        DisableAction(move);
        DisableAction(jump);
        DisableAction(sprint);
    }

    private void UnlockInputs()
    {
        EnableAction(move);
        EnableAction(jump);
        EnableAction(sprint);
    }

    private void DisableAction(InputActionReference ar)
    {
        if (ar?.action == null) return;
        if (ar.action.enabled) ar.action.Disable();
    }

    private void EnableAction(InputActionReference ar)
    {
        if (ar?.action == null) return;
        if (!ar.action.enabled) ar.action.Enable();
    }

    public void RespawnFinished()
    {
        if (trapRoutine != null)
        {
            StopCoroutine(trapRoutine);
            trapRoutine = null;
        }

        if (visualRoot != null)
            visualRoot.SetActive(true);

        UnlockInputs();

        IsTrapped = false;
        IsInvisible = false;
        InvisibleStartTime = -1f;
    }

    private void OnDisable()
    {
        if (trapRoutine != null)
        {
            StopCoroutine(trapRoutine);
            trapRoutine = null;
        }
    }
}