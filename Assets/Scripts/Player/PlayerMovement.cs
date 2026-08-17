using PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerStaminaHandler staminaHandler;
    [SerializeField] private PlayerInteractionHandler interactHandler;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform detectionTransform;

    [Header("Input Settings")]
    public InputActionReference move;
    public InputActionReference jump;
    public InputActionReference sprint;

    [Header("Movement Settings")]
    [SerializeField] private float moveForce = 5f;
    [SerializeField] private float rotateForce = 5f;
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

    [Header("GroundCheck Settings")]
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private float checkDistance;

    private Vector2 moveInput;
    private float maxSpeedSqr;
    private float moveCost;

    private bool isActive;
    private bool isInputPresent;
    private bool isSprinting;
    private bool isJumping;
    private bool isGrounded;
    private bool wasGrounded;

    private void OnEnable()
    {
        if (move != null && move.action != null)
        {
            move.action.Enable();
        }

        if (jump != null && jump.action != null)
        {
            jump.action.Enable();
        }

        if (sprint != null && sprint.action != null)
        {
            sprint.action.Enable();
        }

        isActive = true;
        isSprinting = false;
        isJumping = false;
        wasGrounded = true;
        maxSpeedSqr = maxAllowableVelocity * maxAllowableVelocity;
        rb.linearDamping = defaultMoveDamping;
        rb.angularDamping = defaultRotationDamping;
        moveCost = baseMoveCost;
    }

    private void OnDisable()
    {
        if (move != null && move.action != null)
        {
            move.action.Disable();
        }

        if (jump != null && jump.action != null)
        {
            jump.action.Disable();
        }

        if (sprint != null && sprint.action != null)
        {
            sprint.action.Disable();
        }
    }

    private void Update()
    {
        if (!isActive) return;
        CheckMovementInput();
        CheckJumpInput();
    }

    private void FixedUpdate()
    {
        if (!isActive) return;
        GroundCheck();
        if (isJumping) return;
        HandleMovement();
        HandleRotation();
    }

    private void GroundCheck()
    {
        isGrounded = Physics.Raycast(detectionTransform.position, Vector3.down, checkDistance, whatIsGround, QueryTriggerInteraction.Ignore);

        if (isGrounded == wasGrounded) return;
        if (isGrounded) SetJumping(false);

        wasGrounded = isGrounded;
    }

    private void CheckMovementInput()
    {
        if (move == null || move.action == null)
        {
            moveInput = Vector2.zero;
            isInputPresent = false;
            isSprinting = false;
            return;
        }

        moveInput = move.action.ReadValue<Vector2>();
        isInputPresent = moveInput.sqrMagnitude > 0.01f;

        if (!isInputPresent)
        {
            isSprinting = false;
            return;
        }

        isSprinting = sprint != null && sprint.action != null && sprint.action.IsPressed();
    }

    private void CheckJumpInput()
    {
        if (jump == null || jump.action == null)
        {
            return;
        }

        if(jump.action.WasPressedThisFrame()) HandleJump();
    }

    private void HandleMovement()
    {
        if (moveInput.y < 0.01f)
        {
            if (!interactHandler.IsInteracting() && isActive)
            {
                stateMachine.ChangeToIdleState();
            }

            return;
        }

        if (!ShouldApplyForce()) return;

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

        if (!staminaHandler.HasEnoughEnergy(moveCost))
        {
            return;
        }

        staminaHandler.ConsumeEnergy(moveCost);

        rb.AddForce(transform.forward * (force * moveInput.y), ForceMode.Force);

        stateMachine.ChangeToNavigationState();
    }

    private void HandleRotation()
    {
        if (!isInputPresent) return;

        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        Vector3 cross = Vector3.Cross(Vector3.forward, inputDirection);
        rb.AddTorque(transform.up * cross.y * rotateForce, ForceMode.Acceleration);
    }

    private void HandleJump()
    {
        if (!isGrounded) return;
        if (isJumping) return;

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
            rb.linearDamping = slipperyMoveDamping;
            rb.angularDamping = slipperyRotationDamping;
        }
        else
        {
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
    #endregion
}