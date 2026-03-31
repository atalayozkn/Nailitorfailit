using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerScripts
{
    public class PlayerMove : NetworkBehaviour
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

        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float currentEnergy = 100f;
        [SerializeField] private float energyDrainDuration = 8f; // kaç saniyede bitsin
        [SerializeField] private float energyRegenRate = 15f; // saniyede dolum
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
        [SerializeField] private PlayerCarry _playerCarry;

        // Animation state machine for player animation
        private CharacterStateMachine _stateMachine;
        // States
        private IdleState _idleState;
        private RunState _runState;
        private JumpState _jumpState;

        private const int _carryLayerIndex = 1;

        [SerializeField] private bool _isGrounded;
        public bool IsGroundedPublic => _isGrounded;

        // Public property for actual speed
        public float CurrentSpeed => baseSpeed * speedMultiplier;

        private void Awake()
        {
            _stateMachine = new CharacterStateMachine();

            // Initialize states ONCE
            _idleState = new IdleState(_animator);
            _runState = new RunState(_animator, () => rb.linearVelocity.magnitude);
            _jumpState = new JumpState(_animator);

            // Set initial state
            _stateMachine.ChangeState(_idleState);

            FindFirstObjectByType<CameraController>().OnPlayerInitialized(transform);
        }
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();

            _isGrounded = true;
        }
        void Update()
        {
            if (isOwned)
            {
                // 1. Check Ground (Simple Center Raycast)
                // (Using bounds.extents.y gets us to the bottom of the collider regardless of shape)
                Vector3 rayOrigin = col.bounds.center;
                float rayLength = col.bounds.extents.y + groundCheckDist;
                //isGrounded = Physics.Raycast(rayOrigin, Vector3.down, rayLength, groundLayer);

                // 2. Read Jump Input (Must be in Update for WasPressedThisFrame)
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

                    if (jump.action.WasPressedThisFrame())
                    {
                        _stateMachine.ChangeState(_jumpState);

                        HandleJump();
                    }
                }

                SetCarrying(_playerCarry.IsCarrying);

                _stateMachine.Tick();

                HandleSprint();
                HandleEnergy();
                UpdateEnergyUI();
            }
        }
        void FixedUpdate()
        {
            if (!isOwned) return;
            HandleMovement();
        }

        public override void OnStartClient()
        {
            // Use OnNetworkSpawn instead of Start for Netcode initialization
            if (isOwned)
            {
                if (jump != null) jump.action.Enable();
            }
        }

        public override void OnStopClient()
        {
            // Use OnNetworkDespawn instead of OnDisable
            if (isOwned)
            {
                if (jump != null) jump.action.Disable();
            }
        }

        private void HandleSprint()
        {
            if (sprint == null) return;

            bool isMoving = move.action.ReadValue<Vector2>().magnitude > 0.1f;

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

            // Normalize to prevent faster diagonal movement
            if (inputDir.magnitude > 1f) inputDir.Normalize();

            // Move
            Vector3 targetVelocity = inputDir * CurrentSpeed;

            // Maintain current Y velocity (Gravity/Jumping) so we don't snap to ground
            targetVelocity.y = rb.linearVelocity.y;

            // Apply velocity directly (more reliable for platformers than MovePosition)
            // But we Lerp the horizontal values for a tiny bit of weight (optional)
            Vector3 currentVel = rb.linearVelocity;
            Vector3 newVel = Vector3.Lerp(currentVel, targetVelocity, 15f * Time.fixedDeltaTime);
            newVel.y = rb.linearVelocity.y; // Keep gravity pure

            rb.linearVelocity = newVel;

            // Rotate
            if (inputDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(inputDir);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }

        private void HandleJump()
        {
            // Reset Y velocity before jumping to ensure consistent height
            Vector3 vel = rb.linearVelocity;
            vel.y = 0;
            rb.linearVelocity = vel;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _isGrounded = false; // Prevent double jump immediately

            StartCoroutine(CheckForGround());
        }

        //Koþarken enerji azalmasý sistemi
        private void HandleEnergy()
        {
            float drainPerSecond = maxEnergy / energyDrainDuration;

            bool isMoving = move.action.ReadValue<Vector2>().magnitude > 0.1f;
            bool isSprinting = sprint.action.IsPressed() && _isGrounded && isMoving && canSprint;

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
                // regen
                currentEnergy += energyRegenRate * Time.deltaTime;
                currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);

                if (!canSprint && currentEnergy >= sprintUnlockThreshold)
                {
                    canSprint = true;
                }
            }
        }
        //Kola içme sistemi
        public void RefillEnergy()
        {
            currentEnergy = maxEnergy;
            canSprint = true;
        }
        //Enerji barý UI
        private void UpdateEnergyUI()
        {
            if (energySlider != null)
            {
                energySlider.value = EnergyPercent;
            }

            if (energyCanvas != null)
            {
                if (currentEnergy < maxEnergy)
                    energyCanvas.SetActive(true);
                else
                    energyCanvas.SetActive(false);
            }
        }

        /// <summary>
        /// Returns a Vector2 for Animation Blend Trees.
        /// X = Horizontal (Strafing), Y = Vertical (Forward Speed).
        /// Since we rotate to face movement, Y will be high and X will be near 0.
        /// </summary>
        public Vector2 GetAnimationDirection()
        {
            // Convert World Velocity to Local Space
            // If the player is moving North, and facing North, the result is (0, 0, Speed)
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

            // Return normalized values or raw speed depending on your Blend Tree setup
            // This returns the actual velocity values (e.g., 0, 5.0)
            return new Vector2(localVel.x, localVel.z);
        }

        // Debug gizmo to see ground check
        private void OnDrawGizmos()
        {
            if (col != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                // Draw from center down
                Gizmos.DrawLine(col.bounds.center, col.bounds.center + (Vector3.down * (col.bounds.extents.y + groundCheckDist)));
            }
        }
        void SetCarrying(bool isCarrying)
        {
            _animator.SetLayerWeight(_carryLayerIndex, isCarrying ? 0.5f : 0f);
        }

        private IEnumerator CheckForGround()
        {
            yield return new WaitForSeconds(0.1f);

            while (!_isGrounded)
            {
                _isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDist, groundMask);

                yield return null;
            }
        }
    }
}