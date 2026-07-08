using PlayerScripts;
using UnityEngine;

public enum PlayerStates
{
    Idle,
    Navigation,
    OnAir,
    Dead,
}

public class PlayerStateMachine_SP : StateMachine_Player
{
    [Header("References")]
    [SerializeField] private PlayerMove_SP playerMove;
    [SerializeField] private Rigidbody rb;

    [field: SerializeField] public PlayerStates currentState { get; private set; }
    [field: SerializeField] public Animator animator { get; private set; }

    [field: SerializeField] public PlayerInteract_SP interactionHandler { get; private set; }

    [Header("Movement Data")]
    [field: SerializeField] public bool isMoving { get; private set; }
    [field: SerializeField] public bool isRunning { get; private set; }
    [field: SerializeField] public bool isJumping { get; private set; }
    [field: SerializeField] public bool isDead { get; private set; }

    [Header("Ground Check")]
    [field: SerializeField] public bool IsGrounded { get; private set; }
    [field: SerializeField] public LayerMask whatIsGround { get; private set; }
    [SerializeField] private float maxDistance = 1.1f;

    private void Awake()
    {
        if (playerMove == null)
            playerMove = GetComponent<PlayerMove_SP>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (interactionHandler == null)
            interactionHandler = GetComponent<PlayerInteract_SP>();

        CheckGround();
    }

    private void Start()
    {
        currentState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState_SP(this));
    }

    public void ChangeToIdleState()
    {
        if (currentState == PlayerStates.Dead)
            return;

        if (currentState == PlayerStates.Idle)
            return;

        currentState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState_SP(this));
    }

    public void ChangeToNavigationState()
    {
        if (currentState == PlayerStates.Dead)
            return;

        if (currentState == PlayerStates.Navigation)
            return;

        currentState = PlayerStates.Navigation;
        SwitchState(new PlayerNavigationState_SP(this));
    }

    public void ChangeToOnAirState()
    {
        if (currentState == PlayerStates.Dead)
            return;

        if (currentState == PlayerStates.OnAir)
            return;

        currentState = PlayerStates.OnAir;
        SwitchState(new PlayerOnAirState_SP(this));
    }

    public void ChangeToDeadState()
    {
        if (currentState == PlayerStates.Dead)
            return;

        currentState = PlayerStates.Dead;
        SwitchState(new PlayerDeadState_SP(this));
    }

    public void ForceSwitchToIdleState()
    {
        currentState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState_SP(this));
    }

    public void ForceSwitchToNavigationState()
    {
        currentState = PlayerStates.Navigation;
        SwitchState(new PlayerNavigationState_SP(this));
    }

    public void ForceSwitchToOnAirState()
    {
        currentState = PlayerStates.OnAir;
        SwitchState(new PlayerOnAirState_SP(this));
    }

    public void ForceSwitchToDeadState()
    {
        currentState = PlayerStates.Dead;
        SwitchState(new PlayerDeadState_SP(this));
    }

    public void SetMoving(bool condition)
    {
        isMoving = condition;
    }

    public void SetRunning(bool condition)
    {
        isRunning = condition;
    }

    public void SetJumping(bool condition)
    {
        isJumping = condition;
    }

    public void SetDead(bool condition)
    {
        isDead = condition;
    }

    public bool IsCarrying()
    {
        return interactionHandler != null && interactionHandler.IsCarrying;
    }

    private bool QueryIsGrounded()
    {
        return Physics.Raycast(
            transform.position,
            Vector3.down,
            maxDistance,
            whatIsGround,
            QueryTriggerInteraction.Ignore
        );
    }

    public void CheckGround()
    {
        bool wasGrounded = IsGrounded;

        IsGrounded = QueryIsGrounded();

        if (!wasGrounded && IsGrounded)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    public float GetAnimDuration(string clipName)
    {
        if (animator == null)
        {
            Debug.LogWarning("Animator atanmadý!");
            return 0f;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("Animator Controller atanmadý!");
            return 0f;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;

            if (clips[i].name == clipName)
                return clips[i].length;
        }

        Debug.LogWarning("Animation clip bulunamadý: " + clipName);
        return 0f;
    }

    public float GetAnimDuration(string clipName, float fallbackDuration)
    {
        float duration = GetAnimDuration(clipName);

        if (duration <= 0f)
            return fallbackDuration;

        return duration;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;

        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.down * maxDistance
        );
    }
}