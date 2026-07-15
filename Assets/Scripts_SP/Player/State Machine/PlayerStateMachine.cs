using PlayerScripts;
using UnityEngine;

public enum PlayerStates
{
    Idle,
    Navigation,
    OnAir,
    Dead,
}
public class PlayerStateMachine : StateMachine_Player
{
    [Header("References")]
    [field: SerializeField] public PlayerMovement playerMove { get; private set; }
    [field: SerializeField] public Transform detectionTransform { get; private set; }
    [field: SerializeField] public Rigidbody rb { get; private set; }
    [field: SerializeField] public PlayerStates currentPlayerState { get; private set; }
    [field: SerializeField] public Animator animator { get; private set; }
    [field: SerializeField] public PlayerInteractionHandler interactionHandler { get; private set; }

    [Header("Movement Data")]
    [field: SerializeField] public bool isMoving { get; private set; }
    [field: SerializeField] public bool isRunning { get; private set; }
    [field: SerializeField] public bool isJumping { get; private set; }
    [field: SerializeField] public bool isDead { get; private set; }

    [Header("Ground Check")]
    [field: SerializeField] public bool IsGrounded { get; private set; }
    [field: SerializeField] public LayerMask whatIsGround { get; private set; }
    [field: SerializeField] public float maxDistance { get; private set; }

    [field: SerializeField] public bool debugMode;
    
    private void Awake()
    {
        IsGrounded = true;
    }
    private void OnEnable()
    {
        isDead = false;
        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }

    private void OnDisable()
    {
        isMoving = false;
        isRunning = false;
        isJumping = false;
    }

    #region STATES
    public void ChangeToIdleState()
    {
        if (currentPlayerState == PlayerStates.Dead)
            return;

        if (currentPlayerState == PlayerStates.Idle)
            return;

        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }
    public void ChangeToNavigationState()
    {
        if (currentPlayerState == PlayerStates.Dead)
            return;

        if (currentPlayerState == PlayerStates.Navigation)
            return;

        currentPlayerState = PlayerStates.Navigation;
        SwitchState(new PlayerNavigationState(this));
    }
    public void ChangeToOnAirState()
    {
        if (currentPlayerState == PlayerStates.Dead)
            return;

        if (currentPlayerState == PlayerStates.OnAir)
            return;

        currentPlayerState = PlayerStates.OnAir;
        SwitchState(new PlayerOnAirState(this));
    }
    public void ChangeToDeadState()
    {
        if (currentPlayerState == PlayerStates.Dead)
            return;

        currentPlayerState = PlayerStates.Dead;
        SwitchState(new PlayerDeadState(this));
    }
    public void ForceSwitchToIdleState()
    {
        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }
    public void ForceSwitchToNavigationState()
    {
        currentPlayerState = PlayerStates.Navigation;
        SwitchState(new PlayerNavigationState(this));
    }
    public void ForceSwitchToOnAirState()
    {
        currentPlayerState = PlayerStates.OnAir;
        SwitchState(new PlayerOnAirState(this));
    }
    public void ForceSwitchToDeadState()
    {
        currentPlayerState = PlayerStates.Dead;
        SwitchState(new PlayerDeadState(this));
    }
    #endregion

    #region UTILITY
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
        return interactionHandler.IsCarrying();
    }
    private bool QueryIsGrounded()
    {
        return Physics.Raycast(
            detectionTransform.position,
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

    #endregion

    #region DEBUG
    private void OnDrawGizmosSelected()
    {
        if (detectionTransform == null)
            return;
        if (!debugMode) return;

        Gizmos.color = Color.green;

        Gizmos.DrawLine(
            detectionTransform.position,
            detectionTransform.position + Vector3.down * maxDistance
        );

        Gizmos.DrawWireSphere(
            detectionTransform.position + Vector3.down * maxDistance,
            0.05f
        );
    }
    #endregion

}