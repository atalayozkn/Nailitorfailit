using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
public enum PlayerStates
{
    Idle,
    Navigation,
    Slipping,
    Stunned,
    Interacting,
    Using,
    Stand,
    ShopInspect,
    OnAir,
    Dead,
}

public class PlayerStateMachine : StateMachine_Player
{
    [Header("References")]
    [field: SerializeField] public bool debugMode { get; private set; }
    [field: SerializeField] public PlayerMovement movementHandler { get; private set; }
    [field: SerializeField] public Transform detectionTransform { get; private set; }
    [field: SerializeField] public Rigidbody rb { get; private set; }
    [field: SerializeField] public PlayerStates currentPlayerState { get; private set; }
    [field: SerializeField] public Animator animator { get; private set; }
    [field: SerializeField] public PlayerInteractionHandler interactionHandler { get; private set; }
    [field: SerializeField] public PlayerCrashHelper crashHelper { get; private set; }
    [field: SerializeField] public CinemachineCamera playerCamera { get; private set; }
    [field: SerializeField] public CinemachineCamera levelStartCamera { get; private set; }
    [field: SerializeField] public Rigidbody[] ragdollRigidBodies { get; private set; }
    [field: SerializeField] public ConfigurableJoint[] ragdollJoints { get; private set; }
    [field: SerializeField] public Collider[] ragdollColliders { get; private set; }
    [field: SerializeField] public SkinnedMeshRenderer playerRenderer { get; private set; }

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
    [field: SerializeField] public Vector3 moveDirection { get; private set; }

    [Header("Death Settings")]
    [field: SerializeField] public DeathReason currentReason { get; private set; }
    public RespawnManager respawnManager { get; private set; }

    [Header("Events")]
    [field: SerializeField] public UnityEvent carDeathEvent { get; private set; }
    [field: SerializeField] public UnityEvent electricityDeathEvent { get; private set; }
    [field: SerializeField] public UnityEvent fireDeathEvent { get; private set; }
    [field: SerializeField] public UnityEvent startSlipEvent { get; private set; }
    [field: SerializeField] public UnityEvent stopSlipEvent { get; private set; }
    [field: SerializeField] public UnityEvent onStunStartEvent { get; private set; }
    [field: SerializeField] public UnityEvent onStunEndEvent { get; private set; }

    private Transform initialTarget;
    private void Awake()
    {
        respawnManager = FindAnyObjectByType<RespawnManager>();
        initialTarget = playerCamera.Follow;
        movementHandler.SetActivity(false);
        interactionHandler.SetActivity(false);
        Invoke(nameof(SwitchToPlayerCamera), 1f);
        Invoke(nameof(EnablePlayerInputs), 6f);

        //Disable of Ragdoll
        foreach (var joint in ragdollJoints) joint.enableCollision = false;
        foreach (var col in ragdollColliders) col.enabled = false;
        foreach (var rb in ragdollRigidBodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.detectCollisions = false;
            rb.useGravity = false;
        }
    }
    private void OnEnable()
    {
        currentReason = DeathReason.None;
        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }

    #region STATES

    // Player'ý Idle state'ine geçirmek için çalýþýr.
    // Geçiþe engel bir state yoksa SwitchState() ile PlayerIdleState'e geçer.
    public void ChangeToIdleState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.Idle) return;
        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }

    // Player'ý Navigation state'ine geçirmek için çalýþýr.
    // Geçiþe engel bir state yoksa SwitchState() ile PlayerNavigationState'e geçer.
    public void ChangeToNavigationState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.Navigation) return;
        currentPlayerState = PlayerStates.Navigation;
        SwitchState(new PlayerNavigationState(this));
    }

    // Player'ý OnAir state'ine geçirmek için çalýþýr.
    // Uygun durumdaysa SwitchState() ile PlayerOnAirState'e geçer.
    public void ChangeToOnAirState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.OnAir) return;
        currentPlayerState = PlayerStates.OnAir;
        SwitchState(new PlayerOnAirState(this));
    }

    // Player'ý Slipping state'ine geçirmek için çalýþýr.
    // Uygun durumdaysa SwitchState() ile PlayerSlippingState'e geçer.
    public void ChangeToSlippingState()
    {
        if (currentPlayerState == PlayerStates.Dead || currentPlayerState == PlayerStates.OnAir || currentPlayerState == PlayerStates.Stunned || currentPlayerState == PlayerStates.Stand) return;
        currentPlayerState = PlayerStates.Slipping;
        SwitchState(new PlayerSlippingState(this));
    }

    // Player'ý Interaction state'ine geçirmek için çalýþýr.
    // Uygun durumdaysa SwitchState() ile PlayerInteractionState'e geçer.
    public void ChangeToInteractState()
    {
        if (currentPlayerState == PlayerStates.Interacting) return;
        if (currentPlayerState == PlayerStates.Dead) return;
        currentPlayerState = PlayerStates.Interacting;
        SwitchState(new PlayerInteractionState(this));
    }

    // Player'ý Use state'ine geçirmek için çalýþýr.
    // Uygun durumdaysa SwitchState() ile PlayerUseState'e geçer.
    public void ChangeToUseState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.Using) return;
        currentPlayerState = PlayerStates.Using;
        SwitchState(new PlayerUseState(this));
    }

    // Player'ý Stunned state'ine geçirmek için çalýþýr.
    // Uygun durumdaysa SwitchState() ile PlayerFaintState'e geçer.
    public void ChangeToStunnedState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        if (currentPlayerState == PlayerStates.Stunned) return;
        currentPlayerState = PlayerStates.Stunned;
        SwitchState(new PlayerStunnedState(this));
    }
    public void ChangeToStandState()
    {
        if (currentPlayerState == PlayerStates.Stand) return;
        if (currentPlayerState == PlayerStates.Dead) return;
        currentPlayerState = PlayerStates.Stand;
        SwitchState(new PlayerStandState(this));
    }
    public void ChangeToShopState()
    {
        if (currentPlayerState == PlayerStates.ShopInspect) return;
        if (currentPlayerState == PlayerStates.Dead) return;
        currentPlayerState = PlayerStates.ShopInspect;
        SwitchState(new PlayerShopState(this));
    }

    // Player'ý Dead state'ine geçirmek için çalýþýr.
    // SwitchState() ile PlayerDeadState'e geçer ve Shop Camera referansýný temizler.
    public void ChangeToDeadState()
    {
        if (currentPlayerState == PlayerStates.Dead) return;
        currentPlayerState = PlayerStates.Dead;
        SwitchState(new PlayerDeadState(this));
    }
    public void ForceUpdateIdle()
    {
        currentPlayerState = PlayerStates.Idle;
        SwitchState(new PlayerIdleState(this));
    }

    #endregion

    #region DEATH REASON

    // Player'ýn ölüm sebebini deðiþtirir.
    // Ayný deðer zaten kayýtlýysa gereksiz assignment yapmadan çýkar.
    public void SetDeathReason(DeathReason reason)
    {
        if (currentReason == reason) return;
        currentReason = reason;
    }

    // Player'ýn ölü olup olmadýðýný belirleyen isDead deðerini deðiþtirir.
    public void SetDead(bool condition)
    {
        isDead = condition;
    }

    #endregion

    #region SLIP

    public bool ShouldRecoverFromSlip()
    {
        Vector3 point1 = transform.position + Vector3.up * slipOffsetMultiplier;
        Vector3 point2 = point1 + Vector3.up * 0.01f;

        bool hitSlippery = Physics.CapsuleCast(point1, point2, slipCheckRadius, Vector3.down, out _, slipCheckDistance, slipperyMask, QueryTriggerInteraction.Collide);
        return !hitSlippery;
    }

    #endregion

    #region CRASH

    // Player'ýn mevcut hýzýnýn crashVelocity sýnýrýný geçip geçmediðini kontrol eder.
    // Sonuca göre PlayerCrashHelper.SetActivity() ile crash sistemini aktif veya pasif hale getirir.
    public void SetCrashActivity()
    {
        if (rb == null || crashHelper == null) return;

        float crashVelocitySqr = crashVelocity * crashVelocity;
        bool crashActive = rb.linearVelocity.sqrMagnitude > crashVelocitySqr;

        crashHelper.SetActivity(crashActive);
    }
    public void UpdateMoveDirection()
    {
        moveDirection = transform.forward;
    }
    public void SetTrackTarget(Transform target)
    {
        playerCamera.Follow = target;
        Invoke(nameof(ReverseTrack), 5.0f);
    }
    private void ReverseTrack()
    {
        playerCamera.Follow = initialTarget;
    }
    private void EnablePlayerInputs()
    {
        movementHandler.SetActivity(true);
        interactionHandler.SetActivity(true);
    }
    private void SwitchToPlayerCamera()
    {
        levelStartCamera.Priority = 0;
    }
    #endregion

    #region DEBUG

    // Player seçiliyken ve debugMode açýkken Slip CapsuleCast alanýný Scene ekranýnda görselleþtirir.
    // Gizmos.DrawWireSphere() ve Gizmos.DrawLine() fonksiyonlarýný kullanýr.
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