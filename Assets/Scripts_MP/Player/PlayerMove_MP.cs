// ============================================================
// File:    PlayerMove_MP.cs
// Author:  Tarık + Murad
// Created: 30-Jun-2026
// Purpose: Handles player movement, jumping, sprinting, and energy management in a multiplayer game using Mirror networking
// ============================================================

using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerScripts
{
    public class PlayerMove_MP : NetworkBehaviour
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
        [SerializeField] private PlayerInteract_MP playerInteract;
        [SerializeField] private float carrySpeedMultiplier = 0.65f;
        private bool IsCarryingObject => playerInteract != null && playerInteract.IsCarrying;

        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float currentEnergy = 100f;
        [SerializeField] private float energyDrainDuration = 8f;
        [SerializeField] private float energyRegenRate = 15f;
        [SerializeField] private float sprintUnlockThreshold = 40f;
        [SerializeField] private float energyTickRate = 0.1f;

        private bool canSprint = true;

        [Header("Energy UI")]
        [SerializeField] private GameObject energyCanvas;
        [SerializeField] private UnityEngine.UI.Slider energySlider;
        public float EnergyPercent => maxEnergy > 0f ? currentEnergy / maxEnergy : 0f;

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

        private Vector2 moveInput;
        private bool sprintHeld;
        private Coroutine energyRoutine;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();

            _stateMachine = new CharacterStateMachine();

            _idleState = new IdleState(_animator);
            _runState = new RunState(_animator, () => rb.linearVelocity.magnitude);
            _jumpState = new JumpState(_animator);

            _stateMachine.ChangeState(_idleState);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // Kamera ve input sadece KENDİ oyuncunda aktif edilir
            CameraController cameraController = FindFirstObjectByType<CameraController>();
            if (cameraController != null)
                cameraController.OnPlayerInitialized(transform);

            if (move != null) move.action.Enable();
            if (jump != null) jump.action.Enable();
            if (sprint != null) sprint.action.Enable();
        }

        private void Start()
        {
            if (playerInteract == null)
                playerInteract = GetComponent<PlayerInteract_MP>();

            _isGrounded = true;

            UpdateEnergyUI();

            energyRoutine = StartCoroutine(EnergyRoutine());
        }

        private void OnDisable()
        {
            if (move != null) move.action.Disable();
            if (jump != null) jump.action.Disable();
            if (sprint != null) sprint.action.Disable();

            if (energyRoutine != null)
            {
                StopCoroutine(energyRoutine);
                energyRoutine = null;
            }
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            ReadInput();

            HandleJumpInput();
            HandleAnimation();
            HandleSprint();

            SetCarrying(IsCarryingObject);
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;

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

            if (_isGrounded)
            {
                if (isMoving)
                {
                    _stateMachine.ChangeState(_runState);
                }
                else if (_stateMachine.CurrentState == _runState)
                {
                    _stateMachine.ChangeState(_idleState);
                }
            }

            _stateMachine.Tick();
        }

        private void HandleSprint()
        {
            bool isMoving = moveInput.magnitude > 0.1f;

            if (IsCarryingObject)
            {
                speedMultiplier = carrySpeedMultiplier;
                return;
            }

            if (sprintHeld && _isGrounded && isMoving && canSprint)
                speedMultiplier = sprintMultiplier;
            else
                speedMultiplier = 1f;
        }

        public void SetSpeedModifier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 0.2f, 1.0f);
        }

        private void HandleMovement()
        {
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
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _isGrounded = false;

            StartCoroutine(CheckForGround());
        }

        private IEnumerator EnergyRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(energyTickRate);

            while (true)
            {
                HandleEnergyTick();
                UpdateEnergyUI();

                yield return wait;
            }
        }

        private void HandleEnergyTick()
        {
            float drainPerSecond = maxEnergy / energyDrainDuration;
            bool isMoving = moveInput.magnitude > 0.1f;

            bool isSprinting =
                !IsCarryingObject &&
                sprintHeld &&
                _isGrounded &&
                isMoving &&
                canSprint;

            if (isSprinting)
            {
                currentEnergy -= drainPerSecond * energyTickRate;

                if (currentEnergy <= 0f)
                {
                    currentEnergy = 0f;
                    canSprint = false;
                }
            }
            else
            {
                currentEnergy += energyRegenRate * energyTickRate;
                currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

                if (!canSprint && currentEnergy >= sprintUnlockThreshold)
                    canSprint = true;
            }
        }

        public void RefillEnergy()
        {
            currentEnergy = maxEnergy;
            canSprint = true;
            UpdateEnergyUI();
        }

        private void UpdateEnergyUI()
        {
            if (energySlider != null)
                energySlider.value = EnergyPercent;

            if (energyCanvas != null)
                energyCanvas.SetActive(currentEnergy < maxEnergy);
        }

        public Vector2 GetAnimationDirection()
        {
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            return new Vector2(localVel.x, localVel.z);
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
    }
}