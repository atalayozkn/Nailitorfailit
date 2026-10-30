using PlayerScripts;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerStaminaHandler staminaHandler;
    [SerializeField] private PlayerInteractionHandler interactHandler;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform detectionTransform;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerCameraFOVHelper fovHelper;

    [Header("Input Settings")]
    public InputActionReference move;
    public InputActionReference jump;
    public InputActionReference sprint;

    [Header("Movement Settings")]
    [SerializeField] private float moveForce = 5f;
    [SerializeField] private float rotateForce = 3f;
    [SerializeField] private float maxRotationMultiplier = 20f;
    [SerializeField] private float carryMultiplier = 0.5f;
    [SerializeField] private float sprintMultiplier = 2.0f;
    [SerializeField] private float maxAllowableVelocity = 7.5f;

    [Header("Movement Cost Settings")]
    [SerializeField] private float baseMoveCost = 1f;
    [SerializeField] private float carryCostMultiplier = 5f;
    [SerializeField] private float sprintCostMultiplier = 2.5f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 5f;

    [Header("Damping Settings")]
    [SerializeField] private float defaultMoveDamping = 5f;
    [SerializeField] private float slipperyMoveDamping = 5f;
    [SerializeField] private float defaultRotationDamping = 0.05f;
    [SerializeField] private float slipperyRotationDamping = 0.01f;
    [SerializeField] private float onAirMoveDamping = 0.01f;
    [SerializeField] private float onAirRotationDamping = 0.01f;

    [Header("Step Settings")]
    [Tooltip("Highest ledge (curb, sidewalk) the player walks up without jumping.")]
    [SerializeField] private float maxStepHeight = 0.45f; // sidewalk curbs are ~0.42m above the road
    [Tooltip("How far in front of the capsule a ledge is detected.")]
    [SerializeField] private float stepCheckDistance = 0.15f;
    [Tooltip("Max height gained per physics step, smooths the climb.")]
    [SerializeField] private float stepClimbPerFrame = 0.08f;

    [Header("GroundCheck Settings")]
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private float checkDistance;
    [SerializeField] private float walkStepDuration = 0.5f;
    [SerializeField] private float runStepDuration = 0.25f;

    [Header("Unity")]
    [SerializeField] private UnityEvent onWoodStepEvent;
    [SerializeField] private UnityEvent onWaterStepEvent;
    [SerializeField] private UnityEvent onJumpEvent;
    [SerializeField] private UnityEvent onLandEvent;

    private Vector2 moveInput;
    private float maxSpeedSqr;
    private float moveCost;
    private float stepCounter;
    private float currentStepDuration;

    private bool isActive;
    private bool isInputPresent;
    private bool isSprinting;
    private bool isJumping;
    private bool isSlipping;
    private bool isGrounded;
    private bool wasGrounded;
    private CapsuleCollider capsule;
    private bool isStepping;
    private float stepTargetY;
    private Vector3 stepDirection;

    private void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();
    }

    private void OnEnable()
    {
        if (move != null && move.action != null) move.action.Enable();
        if (jump != null && jump.action != null) jump.action.Enable();
        if (sprint != null && sprint.action != null) sprint.action.Enable();

        isActive = true;
        isSprinting = false;
        isJumping = false;
        wasGrounded = true;
        isSlipping = false;
        maxSpeedSqr = maxAllowableVelocity * maxAllowableVelocity;
        rb.linearDamping = defaultMoveDamping;
        rb.angularDamping = defaultRotationDamping;
        moveCost = baseMoveCost;
    }

    private void OnDisable()
    {
        if (move != null && move.action != null) move.action.Disable();
        if (jump != null && jump.action != null) jump.action.Disable();
        if (sprint != null && sprint.action != null) sprint.action.Disable();
    }

    private void Update()
    {
        if (!isActive) return;
        stepCounter += Time.deltaTime;
        CheckMovementInput();
        CheckJumpInput();
    }

    private void FixedUpdate()
    {
        if (!isActive) return;
        GroundCheck();
        if (isJumping)
        {
            rb.angularVelocity = Vector3.zero;
            return;
        }
        HandleMovement();
        HandleStepUp();
        HandleRotation();
    }

    private void GroundCheck()
    {
        if (Physics.Raycast(detectionTransform.position, Vector3.down, out RaycastHit hit, checkDistance, whatIsGround, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;

            if (isSprinting) currentStepDuration = runStepDuration;
            else currentStepDuration = walkStepDuration;

            if (stepCounter >= currentStepDuration && isInputPresent)
            {
                stepCounter = 0f;

                if (hit.collider.CompareTag("Water"))
                {
                    onWaterStepEvent?.Invoke();
                    
                }
                else
                {
                    onWoodStepEvent?.Invoke();
                }
            }
        }
        else
        {
            isGrounded = false;
        }

        if (isGrounded == wasGrounded) return;

        if (isGrounded)
        {
            bool landedFromJump = isJumping;

            SetJumping(false);

            if (landedFromJump) onLandEvent?.Invoke();
        }

        wasGrounded = isGrounded;
    }

    private void CheckMovementInput()
    {
        if (move == null || move.action == null)
        {
            moveInput = Vector2.zero;
            isInputPresent = false;
            isSprinting = false;
            fovHelper.SetTargetFOV(60);
            return;
        }

        moveInput = move.action.ReadValue<Vector2>();
        isInputPresent = moveInput.sqrMagnitude > 0.01f;

        if (!isInputPresent)
        {
            isSprinting = false;
            fovHelper.SetTargetFOV(60);
            return;
        }

        if (sprint.action.IsPressed())
        {
            isSprinting = true;
            fovHelper.SetTargetFOV(75);
        }
        else
        {
            isSprinting = false;
            fovHelper.SetTargetFOV(60);
        }
    }

    private void CheckJumpInput()
    {
        if (jump == null || jump.action == null)
        {
            return;
        }

        if (jump.action.WasPressedThisFrame()) HandleJump();

    }

    private void HandleMovement()
    {
        if (!isInputPresent)
        {
            if (!interactHandler.IsInteracting() && isActive && !isSlipping)
            {
                stateMachine.ChangeToIdleState();
            }

            return;
        }

        if (!ShouldApplyForce()) return;

        Vector3 moveDirection = GetMovementDirection();
        float force = moveForce;
        moveCost = baseMoveCost;

        if (isSprinting)
        {
            force *= sprintMultiplier;
            moveCost *= sprintCostMultiplier;
        }

        if (interactHandler.IsCarrying())
        {
            force *= carryMultiplier;
            moveCost *= carryCostMultiplier;
        }

        if (!isSprinting && !interactHandler.IsCarrying())
        {
            moveCost = 0f;
        }

        if (!staminaHandler.HasEnoughEnergy(moveCost)) return;
        staminaHandler.ConsumeEnergy(moveCost);

        rb.AddForce(moveDirection * force, ForceMode.Force);

        if (isSlipping) return;
        stateMachine.ChangeToNavigationState();
    }

    private void HandleRotation()
    {
        if (!isInputPresent) return;

        Vector3 moveDirection = GetMovementDirection();

        float angle = Vector3.Angle(transform.forward, moveDirection);

        if (angle > 170f)
        {
            rb.AddTorque(transform.up * rotateForce * maxRotationMultiplier, ForceMode.Acceleration);
            return;
        }

        Vector3 cross = Vector3.Cross(transform.forward, moveDirection);

        float normalizedAngle = angle / 180f;
        float rotationMultiplier = Mathf.Lerp(1f, maxRotationMultiplier, normalizedAngle * normalizedAngle);

        rb.AddTorque(transform.up * cross.y * rotateForce * rotationMultiplier, ForceMode.Acceleration);
    }

    // Lifts the player onto low ledges (curbs, sidewalks) instead of getting stuck against them.
    // The climb keeps a target height and finishes over a few physics steps: once we are lifted the
    // short ground ray stops reaching the lower ground, so it can not depend on isGrounded.
    private void HandleStepUp()
    {
        if (!isInputPresent || isSlipping)
        {
            isStepping = false;
            return;
        }

        if (isStepping)
        {
            ContinueStepUp();
            return;
        }

        if (!isGrounded) return;

        Vector3 direction = GetMovementDirection();
        if (direction.sqrMagnitude < 0.01f) return;

        float radius = capsule != null ? capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.3f;
        Vector3 feet = rb.position;
        float reach = radius + stepCheckDistance;

        // Something blocks us just above the floor...
        if (!Physics.Raycast(feet + Vector3.up * 0.05f, direction, reach, whatIsGround, QueryTriggerInteraction.Ignore)) return;
        // ...but nothing at step height, so it is a ledge and not a wall.
        if (Physics.Raycast(feet + Vector3.up * (maxStepHeight + 0.05f), direction, reach + 0.1f, whatIsGround, QueryTriggerInteraction.Ignore)) return;

        // Find the top of the ledge right in front of the capsule.
        Vector3 probe = feet + direction * reach + Vector3.up * (maxStepHeight + 0.05f);
        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, maxStepHeight + 0.05f, whatIsGround, QueryTriggerInteraction.Ignore)) return;

        float stepHeight = top.point.y - feet.y;
        if (stepHeight <= 0.01f || stepHeight > maxStepHeight) return;

        isStepping = true;
        stepTargetY = top.point.y + 0.02f;
        stepDirection = direction;
        ContinueStepUp();
    }

    private void ContinueStepUp()
    {
        float remaining = stepTargetY - rb.position.y;
        if (remaining <= 0f)
        {
            // On top: nudge forward so the capsule settles on the ledge instead of its edge.
            rb.position += stepDirection * 0.05f;
            isStepping = false;
            return;
        }

        rb.position += Vector3.up * Mathf.Min(remaining, stepClimbPerFrame);
        // Drop the downward velocity so gravity does not pull us back off the edge.
        if (rb.linearVelocity.y < 0f) rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    private void HandleJump()
    {
        if (!isGrounded) return;
        if (isJumping) return;
        if (interactHandler.IsCarrying()) return;
        onJumpEvent?.Invoke();
        SetJumping(true);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    #region UTILITIES

    public bool IsRunning()
    {
        return isSprinting;
    }
    public bool IsMoving()
    {
        return moveInput.sqrMagnitude > 0.01f;
    }
    public bool IsGrounded()
    {
        return isGrounded;
    }
    public void SetActivity(bool condition)
    {
        isActive = condition;
    }
    public void SetSlipping(bool condition)
    {
        if (condition)
        {
            isSlipping = true;
            rb.linearDamping = slipperyMoveDamping;
            rb.angularDamping = slipperyRotationDamping;
        }
        else
        {
            isSlipping = false;
            rb.linearDamping = defaultMoveDamping;
            rb.angularDamping = defaultRotationDamping;
        }
    }
    public void SetJumping(bool condition)
    {
        if (condition)
        {
            isJumping = true;

            rb.linearDamping = onAirMoveDamping;
            rb.angularDamping = onAirRotationDamping;
            stateMachine.ChangeToOnAirState();
        }
        else
        {
            isJumping = false;
            rb.linearDamping = defaultMoveDamping;
            rb.angularDamping = defaultRotationDamping;
        }
    }
    private bool ShouldApplyForce()
    {
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        return horizontalVelocity.sqrMagnitude < maxSpeedSqr;
    }
    private Vector3 GetMovementDirection()
    {
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Ignore camera pitch.
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        return direction.normalized;
    }
    #endregion
}