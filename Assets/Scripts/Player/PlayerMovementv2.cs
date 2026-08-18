using PlayerScripts;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementv2 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerInteractionHandler interactHandler;
    [SerializeField] private Rigidbody rb;

    [Header("Input Settings")]
    public InputActionReference move;
    public InputActionReference jump;
    public InputActionReference sprint;

    [Header("Movement Settings")]
    [SerializeField] private float defaultMoveDamping = 5f;
    [SerializeField] private float slipperyMoveDamping = 5f;
    [SerializeField] private float defaultRotationDamping = 0.05f;
    [SerializeField] private float slipperyRotationDamping = 0.01f;

    private Vector2 moveInput;
    private bool isActive;
    private bool isInputPresent;
    private bool isSprinting;
    private bool isJumping;
    private void OnEnable()
    {
        if (move.action != null) move.action.Enable();
        if (jump.action != null) jump.action.Enable();
        if (sprint.action != null) sprint.action.Enable();

        isActive = true;
        isSprinting = false;
        isJumping = false;
    }
    private void OnDisable()
    {
        if (move.action != null) move.action.Disable();
        if (jump.action != null) jump.action.Disable();
        if (sprint.action != null) sprint.action.Disable();
    }
    private void Update()
    {
        CheckMovementInput();
        CheckJumpInput();
    }
    private void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
    }
    private void CheckMovementInput()
    {
        //Check if movement input is given or not. Set moveInput
        //If no movement input is present moveInput 0, do not look for sprint set it false directly
        //Check if the  sprint input is given or not. Set sprint
    }
    private void CheckJumpInput()
    {
        //Check for jump input
        //Set jump bool
    }
    private void HandleMovement()
    {

    }
    private void HandleJump()
    {

    }

    #region UTILITIES

    public bool IsRunning()
    {
        return isSprinting;
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
    #endregion

}

