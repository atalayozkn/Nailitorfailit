using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class DogMovementHandler : MonoBehaviour
{
    // References
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform bedTransform;
    [SerializeField] private Transform bowlTransform;

    // Movement Settings
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float runSpeed = 6.0f;
    private Transform virtualTarget;

    private bool isMoving;
    private bool shouldRun;
    private Coroutine moveRoutine;

    private const float StallTimeout = 2.5f;
    private const float StallProgressDistance = 0.1f;

    public static readonly int runHash = Animator.StringToHash("Run");
    public static readonly int walkHash = Animator.StringToHash("Walk");
    private void Awake()
    {
        InitializePosition();
    }

    private void InitializePosition()
    {
        if (agent == null) return;

        Ray ray = new Ray(transform.position + Vector3.up, Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, 5f)) return;

        transform.position = hit.point;

        if (NavMesh.SamplePosition(transform.position,out NavMeshHit navHit,1f,NavMesh.AllAreas))
        {
            agent.Warp(navHit.position);
        }
    }
    private void OnEnable()
    {
        StopAllMovement();
    }

    private void OnDisable()
    {
        StopAllMovement();
    }

    public void StopAllMovement()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        // ResetPath on a disabled / off-mesh agent only logs an error; there is no path to clear then.
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        isMoving = false;
        shouldRun = false;
    }

    public void SetTarget(Transform target)
    {
        virtualTarget = target;
    }

    public void SetTargetAsBed()
    {
        SetTarget(bedTransform);
    }

    public float CheckDistanceToBed()
    {
        return CheckDistanceToTarget(bedTransform);
    }

    public void SetTargetAsBowl()
    {
        SetTarget(bowlTransform);
    }

    public float CheckDistanceToBowl()
    {
        return CheckDistanceToTarget(bowlTransform);
    }

    public float CheckDistanceToTarget(Transform target)
    {
        if (target == null) return Mathf.Infinity;

        Vector3 difference = target.position - transform.position;
        difference.y = 0f;

        return difference.magnitude;
    }
    public void MoveTowardsTarget()
    {
        if (virtualTarget == null && isMoving) return;

        // Not placed on a NavMesh yet (e.g. spawned before the mesh was ready): SetDestination would
        // fail and log an error every frame while the state machine keeps retrying.
        if (virtualTarget == null || !agent.isOnNavMesh) return;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        agent.speed = shouldRun ? runSpeed : walkSpeed;
        agent.SetDestination(virtualTarget.position);

        moveRoutine = StartCoroutine(MovementRoutine());
    }
    public bool IsMoving()
    {
        return isMoving;
    }
    public void SetRunning(bool condition)
    {
        shouldRun = condition;

        if (isMoving)
        {
            agent.speed = shouldRun ? runSpeed : walkSpeed;
        }
    }
    public void SetBreakDistance(float amount)
    {
        agent.stoppingDistance = amount;
    }
    private IEnumerator MovementRoutine()
    {
        isMoving = true;
        agent.isStopped = false;
        Vector3 position = virtualTarget.position;
        Vector3 lastProgressPosition = transform.position;
        float stalledTime = 0f;

        while ((agent.pathPending || agent.remainingDistance > agent.stoppingDistance) && virtualTarget != null)
        {
            position = virtualTarget.position;
            agent.SetDestination(position);

            // Without this, a blocked agent never reaches the stopping distance and its state waits forever.
            if ((transform.position - lastProgressPosition).sqrMagnitude >= StallProgressDistance * StallProgressDistance)
            {
                lastProgressPosition = transform.position;
                stalledTime = 0f;
            }
            else
            {
                stalledTime += Time.deltaTime;
                if (stalledTime >= StallTimeout)
                {
                    Debug.LogWarning($"[DogMovement] Stalled at {transform.position} going to {position}. " +
                                     $"Path={agent.pathStatus}, remaining={agent.remainingDistance:F2}, " +
                                     $"onNavMesh={agent.isOnNavMesh}, target={virtualTarget.name}", this);
                    break;
                }
            }

            yield return null;
        }

        agent.isStopped = true;
        agent.ResetPath();
        isMoving = false;
        moveRoutine = null;
    }
}