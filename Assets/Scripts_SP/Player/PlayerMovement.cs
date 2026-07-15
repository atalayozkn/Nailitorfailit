using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerScripts
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStaminaHandler stamina;
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private PlayerInteractionHandler playerInteract;
        [SerializeField] private Rigidbody rb;

        [Header("Movement Settings")]
        [SerializeField] private float baseSpeed = 10f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float baseMovementCost = 1f;

        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 5f;

        [Header("Input")]
        public InputActionReference move;
        public InputActionReference jump;
        public InputActionReference sprint;

        [Header("Sprint Settings")]
        [SerializeField] private float sprintMoveSpeedMultiplier = 2f;
        [SerializeField] private float sprintMoveCostMultiplier = 1.25f;

        [Header("Carrying Pickup")]
        [SerializeField] private float carryMoveSpeedMultiplier = 0.5f;
        [SerializeField] private float carryMoveCostMultiplier = 1.25f;

        [Header("Movement Cost")]
        [SerializeField] private float movementCostInterval = 0.05f;

        private float movementMultiplier = 1f;
        private float externalSpeedModifier = 1f;
        private bool isCarryingObject;

        private float currentMovementCost;
        private Coroutine movementCostRoutine;

        public float CurrentSpeed => baseSpeed * movementMultiplier * externalSpeedModifier;

        private Vector2 moveInput;
        private bool sprintHeld;

        private void Awake()
        {

        }
        private void OnEnable()
        {
            if (move.action != null)
                move.action.Enable();

            if (jump.action != null)
                jump.action.Enable();

            if (sprint.action != null)
                sprint.action.Enable();
        }
        private void OnDisable()
        {
            if (move.action != null)
                move.action.Disable();

            if (jump.action != null)
                jump.action.Disable();

            if (sprint.action != null)
                sprint.action.Disable();

            StopMovementCostRoutine();
        }
        private void Update()
        {
            HandleInput();
            HandleJumpInput();
        }
        private void FixedUpdate()
        {
            HandleMovement();
        }
        private void HandleInput()
        {
            if (move.action != null)
                moveInput = move.action.ReadValue<Vector2>();
            else
                moveInput = Vector2.zero;

            if (sprint.action != null)
                sprintHeld = sprint.action.IsPressed();
            else
                sprintHeld = false;

            bool isMoving = moveInput.magnitude >= 0.1f;
            isCarryingObject = playerInteract.IsCarrying();

            stateMachine.SetMoving(isMoving);

            bool shouldSprint =
                sprintHeld &&
                isMoving &&
                stamina.HasEnoughEnergy(baseMovementCost * sprintMoveCostMultiplier);

            movementMultiplier = 1f;

            if (shouldSprint)
                movementMultiplier *= sprintMoveSpeedMultiplier;

            if (isCarryingObject)
                movementMultiplier *= carryMoveSpeedMultiplier;

            currentMovementCost = baseMovementCost;

            if (shouldSprint)
                currentMovementCost *= sprintMoveCostMultiplier;

            if (isCarryingObject)
                currentMovementCost *= carryMoveCostMultiplier;

            if (isMoving)
                StartMovementCostRoutine();
            else
                StopMovementCostRoutine();

            stateMachine.SetRunning(shouldSprint);

            UpdateGroundStateOnly(isMoving);
        }
        private void UpdateGroundStateOnly(bool isMoving)
        {
            if (isMoving)
                stateMachine.ChangeToNavigationState();
            else
                stateMachine.ChangeToIdleState();
        }
        private void HandleJumpInput()
        {
            if (isCarryingObject)
                return;

            if (!jump.action.WasPressedThisFrame())
                return;

            if (!stateMachine.IsGrounded)
                return;

            Jump();

            stateMachine.ChangeToOnAirState();
        }
        private void HandleMovement()
        {
            if (stateMachine.currentPlayerState == PlayerStates.Dead)
                return;

            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);

            if (inputDir.magnitude > 1f)
                inputDir.Normalize();

            Vector3 targetVelocity = inputDir * CurrentSpeed;
            targetVelocity.y = rb.linearVelocity.y;

            Vector3 currentVelocity = rb.linearVelocity;

            Vector3 newVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                15f * Time.fixedDeltaTime);

            newVelocity.y = rb.linearVelocity.y;

            rb.linearVelocity = newVelocity;

            if (inputDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(inputDir);

                rb.rotation = Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime);
            }
        }
        private void Jump()
        {
            if (rb == null)
                return;

            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;
            rb.linearVelocity = velocity;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        private void StartMovementCostRoutine()
        {
            if (movementCostRoutine != null)
                return;

            movementCostRoutine = StartCoroutine(MovementCostRoutine());
        }
        private void StopMovementCostRoutine()
        {
            if (movementCostRoutine == null)
                return;

            StopCoroutine(movementCostRoutine);
            movementCostRoutine = null;
        }
        private IEnumerator MovementCostRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(movementCostInterval);

            while (true)
            {
                stamina.ConsumeEnergy(currentMovementCost);
                yield return wait;
            }
        }
        public void SetSpeedModifier(float multiplier)
        {
            externalSpeedModifier = Mathf.Clamp(multiplier, 0.2f, 1.0f);
        }
    }
}