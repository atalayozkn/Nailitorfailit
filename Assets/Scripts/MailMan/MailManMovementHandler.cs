using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class MailManMovementHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private MailManController controller;

    private bool isMoving;
    private Coroutine movementCoroutine;

    public void OnEnable()
    {
        isMoving = false;

        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
    }
    public void MoveToTransform(Transform target)
    {
        if (target == null) return;

        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        isMoving = true;
        agent.isStopped = false;
        agent.SetDestination(target.position);
        movementCoroutine = StartCoroutine(MovementRoutine());
    }
    public void WarpTo(Vector3 position)
    {
        bool result = agent.Warp(position);
    }
    private IEnumerator MovementRoutine()
    {
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null;
        }

        isMoving = false;
        agent.isStopped = true;
        movementCoroutine = null;
        controller.HandleMovementComplete();
    }
    public void StopMovement()
    {
        agent.isStopped = true;
    }
    public void SetBreakDistance(float amount)
    {
        agent.stoppingDistance = amount;
    }
    public void SetMoveSpeed(float speed)
    {
        agent.speed = speed;
    }
    public bool IsMoving()
    {
        return isMoving;
    }
}