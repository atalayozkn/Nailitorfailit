using PlayerScripts;
using UnityEngine;

public enum PlayerStates
{
    Idle,
    Navigation,
    Slipping,
    Stunned,
    Interacting,
    Using,
    OnAir,
    Dead,
}

public class PlayerStateMachine : StateMachine_Player
{
    [Header("References")]
    [field: SerializeField] public PlayerMovement movementHandler { get; private set; }
    [field: SerializeField] public Transform detectionTransform { get; private set; }
    [field: SerializeField] public Rigidbody rb { get; private set; }
    [field: SerializeField] public PlayerStates currentPlayerState { get; private set; }
    [field: SerializeField] public Animator animator { get; private set; }
    [field: SerializeField] public PlayerInteractionHandler interactionHandler { get; private set; }
    [field: SerializeField] public PlayerCrashHelper crashHelper { get; private set; }

    [Header("Movement Data")] 
    [field: SerializeField] public bool isDead { get; private set; }

    [Header("Use Settings")]
    [field: SerializeField] public float useDuration { get; private set; }

    [Header("Slip Settings")]
    [field: SerializeField] public float slipCheckDistance { get; private set; }
    [field: SerializeField] public float slipOffsetMultiplier { get; private set; }
    [field: SerializeField] public float slipCheckRadius { get; private set; }
    [field: SerializeField] public LayerMask slipperyMask { get; private set; }

    [Header("Crash Settings")]
    [field: SerializeField] public float crashVelocity { get; private set; }
    [field: SerializeField] public bool debugMode;

    [Header("Death Settings")]
    [field: SerializeField] public DeathReason currentReason { get; private set; }
    public RespawnManager respawnManager { get; private set; }

    private void Awake()
    {
        respawnManager = FindAnyObjectByType<RespawnManager>();
    }
    private void OnEnable()
    {
        isDead = false;
        currentReason = DeathReason.None;
        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }

    #region STATES

    public void ChangeToIdleState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.Idle) return;
        if (currentPlayerState == PlayerStates.Slipping) return;
        if (currentPlayerState == PlayerStates.Stunned) return;
        if (currentPlayerState == PlayerStates.OnAir) return;

        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }

    public void ChangeToNavigationState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.Navigation) return;
        if (currentPlayerState == PlayerStates.Slipping) return;
        if (currentPlayerState == PlayerStates.Stunned) return;
        if (currentPlayerState == PlayerStates.OnAir) return;

        currentPlayerState = PlayerStates.Navigation;
        SwitchState(new PlayerNavigationState(this));
    }

    public void ChangeToOnAirState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.OnAir) return;

        currentPlayerState = PlayerStates.OnAir;
        SwitchState(new PlayerOnAirState(this));
    }

    public void ChangeToSlippingState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.OnAir) return;

        currentPlayerState = PlayerStates.Slipping;
        SwitchState(new PlayerSlippingState(this));
    }

    public void ChangeToInteractState()
    {
        if (currentPlayerState == PlayerStates.Interacting) return;
        if (currentPlayerState == PlayerStates.Stunned) return;
        if (currentPlayerState == PlayerStates.Dead) return;

        currentPlayerState = PlayerStates.Interacting;
        SwitchState(new PlayerInteractionState(this));
    }

    public void ChangeToUseState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.Using) return;

        currentPlayerState = PlayerStates.Using;
        SwitchState(new PlayerUseState(this));
    }

    public void ChangeToStunnedState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;

        currentPlayerState = PlayerStates.Stunned;
        SwitchState(new PlayerFaintState(this));
    }

    public void ChangeToDeadState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
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
    public void SetDeathReason(DeathReason reason)
    {
        if (currentReason == reason) return;
        currentReason = reason;
    }
    #endregion

    #region UTILITY

    public void SetDead(bool condition)
    {
        isDead = condition;
    }

    public bool ShouldRecoverFromSlip()
    {
        Vector3 point1 = transform.position + Vector3.up * slipOffsetMultiplier;
        Vector3 point2 = point1 + Vector3.up * 0.01f;
        bool hitSlippery = Physics.CapsuleCast(point1, point2, slipCheckRadius, Vector3.down, out _, slipCheckDistance, slipperyMask, QueryTriggerInteraction.Collide);
        return !hitSlippery;
    }
    public void SetCrashActivity()
    {
        if (rb.linearVelocity.magnitude > crashVelocity) crashHelper.SetActivity(true);
        else crashHelper.SetActivity(false);
    }
    #endregion

    #region DEBUG

    private void OnDrawGizmosSelected()
    {
        if (detectionTransform == null) return;
        if (!debugMode) return;

        Gizmos.color = Color.cyan;
        Vector3 startPoint1 = transform.position + Vector3.up * slipOffsetMultiplier;
        Vector3 startPoint2 = startPoint1 + Vector3.up * 0.01f;
        Vector3 endPoint1 = startPoint1 + Vector3.down * slipCheckDistance;
        Vector3 endPoint2 = startPoint2 + Vector3.down * slipCheckDistance;

        Gizmos.DrawWireSphere(startPoint1, slipCheckRadius);
        Gizmos.DrawWireSphere(startPoint2, slipCheckRadius);

        Gizmos.DrawWireSphere(endPoint1, slipCheckRadius);
        Gizmos.DrawWireSphere(endPoint2, slipCheckRadius);

        Gizmos.DrawLine(startPoint1 + Vector3.right * slipCheckRadius, endPoint1 + Vector3.right * slipCheckRadius);
        Gizmos.DrawLine(startPoint1 - Vector3.right * slipCheckRadius, endPoint1 - Vector3.right * slipCheckRadius);
        Gizmos.DrawLine(startPoint1 + Vector3.forward * slipCheckRadius, endPoint1 + Vector3.forward * slipCheckRadius);
        Gizmos.DrawLine(startPoint1 - Vector3.forward * slipCheckRadius, endPoint1 - Vector3.forward * slipCheckRadius);
    }

    #endregion
}