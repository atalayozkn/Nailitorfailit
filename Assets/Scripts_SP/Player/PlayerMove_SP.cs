using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerScripts
{
    public class PlayerMove_SP : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStamina_SP stamina;
        [SerializeField] private PlayerStateMachine_SP stateMachine;
        [SerializeField] private PlayerInteract_SP playerInteract;
        [SerializeField] private Rigidbody rb;

        [Header("Movement Settings")]
        [SerializeField] private float baseSpeed = 10f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 5f;

        [Header("Input")]
        public InputActionReference move;
        public InputActionReference jump;
        public InputActionReference sprint;

        [Header("Sprint Settings")]
        [SerializeField] private float sprintMultiplier = 2f;

        [Header("Carrying Pickup")]
        [SerializeField] private float carrySpeedMultiplier = 0.65f;

        private bool IsCarryingObject => playerInteract != null && playerInteract.IsCarrying;

        private float movementMultiplier = 1f;
        private float externalSpeedModifier = 1f;

        public float CurrentSpeed => baseSpeed * movementMultiplier * externalSpeedModifier;
        public float EnergyPercent => stamina != null ? stamina.EnergyPercent : 1f;

        private Vector2 moveInput;
        private bool sprintHeld;

        private void Awake()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (stamina == null)
                stamina = GetComponent<PlayerStamina_SP>();

            if (stateMachine == null)
                stateMachine = GetComponent<PlayerStateMachine_SP>();

            if (playerInteract == null)
                playerInteract = GetComponent<PlayerInteract_SP>();

            CameraController cameraController = FindFirstObjectByType<CameraController>();
            if (cameraController != null)
                cameraController.OnPlayerInitialized(transform);
        }

        private void OnEnable()
        {
            if (move != null && move.action != null)
                move.action.Enable();

            if (jump != null && jump.action != null)
                jump.action.Enable();

            if (sprint != null && sprint.action != null)
                sprint.action.Enable();
        }

        private void OnDisable()
        {
            if (move != null && move.action != null)
                move.action.Disable();

            if (jump != null && jump.action != null)
                jump.action.Disable();

            if (sprint != null && sprint.action != null)
                sprint.action.Disable();

            if (stamina != null)
                stamina.SetSprinting(false);

            if (stateMachine != null)
            {
                stateMachine.SetMoving(false);
                stateMachine.SetRunning(false);
            }
        }

        private void Update()
        {
            HandleInput();
            HandleJumpInput();
        }

        private void FixedUpdate()
        {
            if (stateMachine != null)
                stateMachine.CheckGround();

            HandleMovement();
        }

        private void HandleInput()
        {
            if (move != null && move.action != null)
                moveInput = move.action.ReadValue<Vector2>();
            else
                moveInput = Vector2.zero;

            if (sprint != null && sprint.action != null)
                sprintHeld = sprint.action.IsPressed();
            else
                sprintHeld = false;

            bool isMoving = moveInput.magnitude >= 0.1f;

            if (stateMachine != null)
                stateMachine.SetMoving(isMoving);

            if (IsCarryingObject)
            {
                movementMultiplier = carrySpeedMultiplier;

                if (stamina != null)
                    stamina.SetSprinting(false);

                if (stateMachine != null)
                    stateMachine.SetRunning(false);

                UpdateGroundStateOnly(isMoving);

                return;
            }

            bool staminaAllowsSprint = stamina == null || stamina.CanSprint;

            bool shouldSprint =
                sprintHeld &&
                isMoving &&
                staminaAllowsSprint;

            movementMultiplier = shouldSprint ? sprintMultiplier : 1f;

            if (stamina != null)
                stamina.SetSprinting(shouldSprint);

            if (stateMachine != null)
                stateMachine.SetRunning(shouldSprint);

            UpdateGroundStateOnly(isMoving);
        }

        private void UpdateGroundStateOnly(bool isMoving)
        {
            if (stateMachine == null)
                return;

            if (stateMachine.currentState == PlayerStates.Dead)
                return;

            if (stateMachine.currentState == PlayerStates.OnAir)
                return;

            if (isMoving)
                stateMachine.ChangeToNavigationState();
            else
                stateMachine.ChangeToIdleState();
        }

        private void HandleJumpInput()
        {
            if (jump == null || jump.action == null)
                return;

            if (IsCarryingObject)
                return;

            if (!jump.action.WasPressedThisFrame())
                return;

            if (stateMachine != null && !stateMachine.IsGrounded)
                return;

            HandleJump();

            if (stateMachine != null)
                stateMachine.ChangeToOnAirState();
        }

        private void HandleMovement()
        {
            if (rb == null)
                return;

            if (stateMachine != null && stateMachine.currentState == PlayerStates.Dead)
                return;

            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

            if (inputDir.magnitude > 1f)
                inputDir.Normalize();

            Vector3 targetVelocity = inputDir * CurrentSpeed;
            targetVelocity.y = rb.linearVelocity.y;

            Vector3 currentVel = rb.linearVelocity;

            Vector3 newVel = Vector3.Lerp(
                currentVel,
                targetVelocity,
                15f * Time.fixedDeltaTime
            );

            newVel.y = rb.linearVelocity.y;
            rb.linearVelocity = newVel;

            if (inputDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(inputDir);

                rb.rotation = Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                );
            }
        }

        private void HandleJump()
        {
            if (rb == null)
                return;

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        public void SetSpeedModifier(float multiplier)
        {
            externalSpeedModifier = Mathf.Clamp(multiplier, 0.2f, 1.0f);
        }

        public void RefillEnergy()
        {
            if (stamina != null)
                stamina.RefillEnergy();
        }

        public Vector2 GetAnimationDirection()
        {
            if (rb == null)
                return Vector2.zero;

            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            return new Vector2(localVel.x, localVel.z);
        }
    }
}