using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerScripts
{
    public class PlayerMove_SP : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private Animator _animator;
        [SerializeField] private float baseSpeed = 10f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Jump Settings")]
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float groundCheckDist = 0.05f;
        [SerializeField] private float groundCheckInterval = 0.03f;

        [Header("Input")]
        public InputActionReference move;
        public InputActionReference jump;
        public InputActionReference sprint;

        [Header("Sprint Settings")]
        [SerializeField] private float sprintMultiplier = 2f;

        [Header("Carrying Pickup")]
        [SerializeField] private PlayerInteract_SP playerInteract;
        [SerializeField] private float carrySpeedMultiplier = 0.65f;
        private bool IsCarryingObject => playerInteract != null && playerInteract.IsCarrying;

        [Header("Stamina")]
        [SerializeField] private PlayerStamina_SP stamina;

        private Rigidbody rb;
        private Collider col;

        private float movementStateMultiplier = 1f;
        private float externalSpeedModifier = 1f;

        private CharacterStateMachine _stateMachine;
        private IdleState _idleState;
        private RunState _runState;
        private JumpState _jumpState;

        private const int _carryLayerIndex = 1;

        [SerializeField] private bool _isGrounded;
        public bool IsGroundedPublic => _isGrounded;

        public float CurrentSpeed => baseSpeed * movementStateMultiplier * externalSpeedModifier;
        public float EnergyPercent => stamina != null ? stamina.EnergyPercent : 1f;

        private Vector2 moveInput;
        private bool sprintHeld;

        private Coroutine groundCheckRoutine;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();

            _stateMachine = new CharacterStateMachine();

            _idleState = new IdleState(_animator);
            _runState = new RunState(_animator, () => rb.linearVelocity.magnitude);
            _jumpState = new JumpState(_animator);

            _stateMachine.ChangeState(_idleState);

            CameraController cameraController = FindFirstObjectByType<CameraController>();
            if (cameraController != null)
                cameraController.OnPlayerInitialized(transform);
        }

        private void Start()
        {
            if (playerInteract == null)
                playerInteract = GetComponent<PlayerInteract_SP>();

            if (stamina == null)
                stamina = GetComponent<PlayerStamina_SP>();

            _isGrounded = CheckGrounded();
        }

        private void OnEnable()
        {
            if (move != null) move.action.Enable();
            if (jump != null) jump.action.Enable();
            if (sprint != null) sprint.action.Enable();

            StartGroundCheckRoutine();
        }

        private void OnDisable()
        {
            if (move != null) move.action.Disable();
            if (jump != null) jump.action.Disable();
            if (sprint != null) sprint.action.Disable();

            StopGroundCheckRoutine();

            if (stamina != null)
                stamina.SetSprinting(false);
        }

        private void Update()
        {
            ReadInput();

            HandleJumpInput();
            HandleAnimation();
            HandleSprint();

            SetCarrying(IsCarryingObject);
        }

        private void FixedUpdate()
        {
            HandleMovement();
        }

        private void ReadInput()
        {
            if (move != null && move.action != null)
                moveInput = move.action.ReadValue<Vector2>();

            if (sprint != null && sprint.action != null)
                sprintHeld = sprint.action.IsPressed();
        }

        private void HandleJumpInput()
        {
            if (!_isGrounded) return;
            if (jump == null || jump.action == null) return;
            if (IsCarryingObject) return;

            if (jump.action.WasPressedThisFrame())
            {
                _stateMachine.ChangeState(_jumpState);
                HandleJump();
            }
        }

        private void HandleAnimation()
        {
            bool isMoving = moveInput.magnitude > 0.1f;

            if (!_isGrounded)
            {
                if (_stateMachine.CurrentState != _jumpState)
                    _stateMachine.ChangeState(_jumpState);

                _stateMachine.Tick();
                return;
            }

            if (isMoving)
            {
                _stateMachine.ChangeState(_runState);
            }
            else if (_stateMachine.CurrentState == _runState || _stateMachine.CurrentState == _jumpState)
            {
                _stateMachine.ChangeState(_idleState);
            }

            _stateMachine.Tick();
        }

        private void HandleSprint()
        {
            bool isMoving = moveInput.magnitude > 0.1f;

            if (IsCarryingObject)
            {
                movementStateMultiplier = carrySpeedMultiplier;

                if (stamina != null)
                    stamina.SetSprinting(false);

                return;
            }

            bool staminaAllowsSprint = stamina == null || stamina.CanSprint;

            bool shouldSprint =
                sprintHeld &&
                _isGrounded &&
                isMoving &&
                staminaAllowsSprint;

            movementStateMultiplier = shouldSprint ? sprintMultiplier : 1f;

            if (stamina != null)
                stamina.SetSprinting(shouldSprint);
        }

        public void SetSpeedModifier(float multiplier)
        {
            externalSpeedModifier = Mathf.Clamp(multiplier, 0.2f, 1.0f);
        }

        private void HandleMovement()
        {
            if (rb == null) return;

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
            if (rb == null) return;

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            _isGrounded = false;
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

        private void StartGroundCheckRoutine()
        {
            if (groundCheckRoutine != null)
                return;

            groundCheckRoutine = StartCoroutine(GroundCheckRoutine());
        }

        private void StopGroundCheckRoutine()
        {
            if (groundCheckRoutine != null)
            {
                StopCoroutine(groundCheckRoutine);
                groundCheckRoutine = null;
            }
        }

        private IEnumerator GroundCheckRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(groundCheckInterval);

            while (true)
            {
                _isGrounded = CheckGrounded();

                yield return wait;
            }
        }

        private bool CheckGrounded()
        {
            if (col == null)
                return false;

            Vector3 origin = col.bounds.center;
            float distance = col.bounds.extents.y + groundCheckDist;

            return Physics.Raycast(
                origin,
                Vector3.down,
                distance,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private void SetCarrying(bool isCarrying)
        {
            if (_animator == null) return;

            _animator.SetLayerWeight(_carryLayerIndex, isCarrying ? 0.5f : 0f);
        }

        private void OnDrawGizmos()
        {
            Collider drawCollider = col;

            if (drawCollider == null)
                drawCollider = GetComponent<Collider>();

            if (drawCollider == null)
                return;

            Gizmos.color = _isGrounded ? Color.green : Color.red;

            Vector3 origin = drawCollider.bounds.center;
            float distance = drawCollider.bounds.extents.y + groundCheckDist;

            Gizmos.DrawLine(origin, origin + Vector3.down * distance);
        }
    }
}