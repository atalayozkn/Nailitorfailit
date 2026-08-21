using System.Collections;
using UnityEngine;

public class DogMovementHandler : MonoBehaviour
{
    // References
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform bedTransform;
    [SerializeField] private Transform bowlTransform;

    // Rotation Settings
    [Header("Rotation Settings")]
    [SerializeField] private float minRotationTorque;
    [SerializeField] private float maxRotationTorque;

    // Movement Settings
    [Header("Movement Settings")]
    [SerializeField] private float walkForce = 3.0f;
    [SerializeField] private float runForce = 6.0f;
    [SerializeField] private float breakDistance = 0.1f;

    private Transform virtualTarget;

    private bool isMoving;
    private bool isRotating;
    private bool shouldRun;

    private Coroutine rotationRoutine;
    private Coroutine moveRoutine;

    private void OnEnable()
    {
        StopAllMovement();
    }
    private void OnDisable()
    {
        StopAllMovement();
    }
    private void StopAllMovement()
    {
        if (rotationRoutine != null)
        {
            StopCoroutine(rotationRoutine);
            rotationRoutine = null;
        }

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        isRotating = false;
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
        Vector3 difference = bedTransform.position - transform.position;
        difference.y = 0f;

        return difference.magnitude;
    }
    public void SetTargetAsBowl()
    {
        SetTarget(bowlTransform);
    }
    public float CheckDistanceToBowl()
    {
        Vector3 difference = bowlTransform.position - transform.position;
        difference.y = 0f;

        return difference.magnitude;
    }
    public float CheckDistanceToTarget(Transform target)
    {
        Vector3 difference = target.position - transform.position;
        difference.y = 0f;
        return difference.magnitude;
    }
    public void SteerTowardsTarget()
    {
        if (virtualTarget == null || isRotating) return;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
            isMoving = false;
        }

        rotationRoutine = StartCoroutine(RotationRoutine());
    }
    public void MoveTowardsTarget()
    {
        if (virtualTarget == null || isMoving) return;

        if (rotationRoutine != null)
        {
            StopCoroutine(rotationRoutine);
            rotationRoutine = null;
            isRotating = false;
        }

        moveRoutine = StartCoroutine(MovementRoutine());
    }
    public bool IsMoving()
    {
        return isMoving;
    }
    public bool IsRotating()
    {
        return isRotating;
    }
    public void SetRunning(bool condition)
    {
        shouldRun = condition;
    }
    public void SetBreakDistance(float amount)
    {
        breakDistance = amount;
    }
    private IEnumerator RotationRoutine()
    {
        isRotating = true;

        while (virtualTarget != null)
        {
            Vector3 direction = GetTargetDirection();

            if (direction.sqrMagnitude <= 0.0001f) break;

            float remainingAngle = Vector3.Angle(transform.forward, direction);

            if (remainingAngle <= 1f) break;

            float normalizedAngle = Mathf.InverseLerp(0f, 180f, remainingAngle);
            float torque = Mathf.Lerp(minRotationTorque, maxRotationTorque, normalizedAngle);

            ApplyRotationTorque(direction, torque);

            yield return new WaitForFixedUpdate();
        }

        isRotating = false;
        rotationRoutine = null;
    }
    private IEnumerator MovementRoutine()
    {
        isMoving = true;

        float targetForce;

        if (shouldRun) targetForce = runForce;
        else targetForce = walkForce;

        while (true)
        {
            Vector3 direction = GetTargetDirection();

            if (direction.sqrMagnitude <= 0.0001f) break;

            // Stop once we reach the target.
            if (IsAtTarget()) break;

            // Movement force.
            rb.AddForce(direction * targetForce, ForceMode.Force);

            // Constant steering torque.
            ApplyRotationTorque(direction, maxRotationTorque);

            yield return new WaitForFixedUpdate();
        }

        isMoving = false;
        moveRoutine = null;
    }
    private Vector3 GetTargetDirection()
    {
        Vector3 direction = virtualTarget.position - rb.position;
        // Dog movement is restricted to the horizontal plane.
        direction.y = 0f;
        return direction.normalized;
    }
    private void ApplyRotationTorque(Vector3 targetDirection, float torque)
    {
        Vector3 cross = Vector3.Cross(transform.forward, targetDirection);
        rb.AddTorque(Vector3.up * (cross.y * torque), ForceMode.Acceleration);
    }
    private bool IsAtTarget()
    {
        Vector3 difference = virtualTarget.position - rb.position;
        difference.y = 0f;
        return difference.sqrMagnitude <= breakDistance;
    }
}