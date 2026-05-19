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
        [SerializeField] private float groundCheckDist = 0.001f;

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

        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float currentEnergy = 100f;
        [SerializeField] private float energyDrainDuration = 8f;
        [SerializeField] private float energyRegenRate = 15f;
        [SerializeField] private float sprintUnlockThreshold = 40f;
        private bool canSprint = true;

        [Header("Energy UI")]
        [SerializeField] private GameObject energyCanvas;
        [SerializeField] private UnityEngine.UI.Slider energySlider;
        public float EnergyPercent => currentEnergy / maxEnergy;

        [Header("RB , Collider , Etc.")]
        private Rigidbody rb;
        private Collider col;
        private float speedMultiplier = 1f;

        private CharacterStateMachine _stateMachine;
        private IdleState _idleState;
        private RunState _runState;
        private JumpState _jumpState;

        private const int _carryLayerIndex = 1;

        [SerializeField] private bool _isGrounded;
        public bool IsGroundedPublic => _isGrounded;

        public float CurrentSpeed => baseSpeed * speedMultiplier;

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
            {
                cameraController.OnPlayerInitialized(transform);
            }
        }

        private void Start()
        {
            if (playerInteract == null)
                playerInteract = GetComponent<PlayerInteract_SP>();

            _isGrounded = true;

            if (move != null) move.action.Enable();
            if (jump != null) jump.action.Enable();
            if (sprint != null) sprint.action.Enable();

            UpdateEnergyUI();
        }

        private void OnDisable()
        {
            if (move != null) move.action.Disable();
            if (jump != null) jump.action.Disable();
            if (sprint != null) sprint.action.Disable();
        }

        private void Update()
        {
            if (move == null || move.action == null) return;
            if (jump == null || jump.action == null) return;
            if (sprint == null || sprint.action == null) return;

            if (_isGrounded)
            {
                if (CurrentSpeed > 0)
                {
                    _stateMachine.ChangeState(_runState);
                }
                else if (_stateMachine.CurrentState == _runState)
                {
                    _stateMachine.ChangeState(_idleState);
                }

                if (!IsCarryingObject && jump.action.WasPressedThisFrame())
                {
                    _stateMachine.ChangeState(_jumpState);
                    HandleJump();
                }
            }

            _stateMachine.Tick();

            SetCarrying(IsCarryingObject);

            HandleSprint();
            HandleEnergy();
            UpdateEnergyUI();
        }

        private void FixedUpdate()
        {
            HandleMovement();
        }

        private void HandleSprint()
        {
            bool isMoving = move.action.ReadValue<Vector2>().magnitude > 0.1f;

            if (IsCarryingObject)
            {
                speedMultiplier = carrySpeedMultiplier;
                return;
            }

            if (sprint.action.IsPressed() && _isGrounded && isMoving && canSprint)
            {
                speedMultiplier = sprintMultiplier;
            }
            else
            {
                speedMultiplier = 1f;
            }
        }

        public void SetSpeedModifier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 0.2f, 1.0f);
        }

        private void HandleMovement()
        {
            Vector2 input = move.action.ReadValue<Vector2>();
            Vector3 inputDir = new Vector3(input.x, 0, input.y);

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
            Vector3 vel = rb.linearVelocity;
            vel.y = 0;
            rb.linearVelocity = vel;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _isGrounded = false;

            StartCoroutine(CheckForGround());
        }

        private void HandleEnergy()
        {
            float drainPerSecond = maxEnergy / energyDrainDuration;

            bool isMoving = move.action.ReadValue<Vector2>().magnitude > 0.1f;
            bool isSprinting =
                !IsCarryingObject &&
                sprint.action.IsPressed() &&
                _isGrounded &&
                isMoving &&
                canSprint;

            if (isSprinting)
            {
                currentEnergy -= drainPerSecond * Time.deltaTime;

                if (currentEnergy <= 0f)
                {
                    currentEnergy = 0f;
                    canSprint = false;
                }
            }
            else
            {
                currentEnergy += energyRegenRate * Time.deltaTime;
                currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);

                if (!canSprint && currentEnergy >= sprintUnlockThreshold)
                {
                    canSprint = true;
                }
            }
        }

        public void RefillEnergy()
        {
            currentEnergy = maxEnergy;
            canSprint = true;
        }

        private void UpdateEnergyUI()
        {
            if (energySlider != null)
            {
                energySlider.value = EnergyPercent;
            }

            if (energyCanvas != null)
            {
                energyCanvas.SetActive(currentEnergy < maxEnergy);
            }
        }

        public Vector2 GetAnimationDirection()
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            return new Vector2(localVel.x, localVel.z);
        }

        private void OnDrawGizmos()
        {
            if (col != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;

                Gizmos.DrawLine(
                    col.bounds.center,
                    col.bounds.center + Vector3.down * (col.bounds.extents.y + groundCheckDist)
                );
            }
        }

        private void SetCarrying(bool isCarrying)
        {
            if (_animator == null) return;

            _animator.SetLayerWeight(_carryLayerIndex, isCarrying ? 0.5f : 0f);
        }

        private IEnumerator CheckForGround()
        {
            yield return new WaitForSeconds(0.1f);

            while (!_isGrounded)
            {
                _isGrounded = Physics.Raycast(
                    transform.position,
                    Vector3.down,
                    groundCheckDist,
                    groundMask
                );

                yield return null;
            }
        }
    }
}