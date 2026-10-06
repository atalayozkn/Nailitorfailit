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

        if (agent != null)
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

        while ((agent.pathPending || agent.remainingDistance > agent.stoppingDistance) && virtualTarget != null)
        {
            position = virtualTarget.position;
            agent.SetDestination(position);
            yield return null;
        }

        agent.isStopped = true;
        agent.ResetPath();
        isMoving = false;
        moveRoutine = null;
    }
}