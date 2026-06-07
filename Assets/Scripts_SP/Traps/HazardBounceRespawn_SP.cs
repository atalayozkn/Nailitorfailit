using UnityEngine;
using UnityEngine.InputSystem;
using PlayerScripts;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMove_SP))]
public class HazardBounceRespawn_SP : MonoBehaviour
{
    [Header("Trap")]
    [SerializeField] private string trapTag = "Trap";

    [Header("Visual")]
    [SerializeField] private GameObject visualRoot;        // model/mesh parent (Player root değil)

    [Header("Ground Check (Layer)")]
    [SerializeField] private LayerMask groundMask;         // Ground layer buraya
    [SerializeField] private float groundCheckExtra = 0.05f;
    [SerializeField] private Collider playerCollider;      // CapsuleCollider

    [Header("Disable inputs until respawn")]
    [SerializeField] private InputActionReference move;
    [SerializeField] private InputActionReference jump;
    [SerializeField] private InputActionReference sprint;

    [Header("Anti Double Trigger")]
    [SerializeField] private float hitCooldown = 0.05f;

    private Rigidbody rb;
    private PlayerMove playerMove;
    private float lastHitTime = -999f;

    // SpawnPointRespawn bunu izleyecek
    public bool IsTrapped { get; private set; }            // görünmez olduktan sonra true
    public bool IsInvisible { get; private set; }          // visual kapandı mı?
    public float InvisibleStartTime { get; private set; } = -1f;

    private enum TrapState
    {
        None,
        WaitingLeaveGround,   // önce yerden kesilmesini bekle
        WaitingLand           // sonra tekrar yere basmasını bekle
    }

    private TrapState state = TrapState.None;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerMove = GetComponent<PlayerMove>();

        if (playerCollider == null) playerCollider = GetComponent<Collider>();

        // Inspector’dan verilmediyse PlayerMove’dan al
        if (move == null) move = playerMove.move;
        if (jump == null) jump = playerMove.jump;
        if (sprint == null) sprint = playerMove.sprint;
    }

    void Update()
    {
        if (state == TrapState.WaitingLeaveGround)
        {
            // ✅ Önce gerçekten havaya çıktı mı?
            if (!IsGroundedByCollider())
            {
                state = TrapState.WaitingLand;
            }
        }
        else if (state == TrapState.WaitingLand)
        {
            // ✅ Havaya çıktıktan SONRA tekrar yere bastıysa görünmez yap
            if (IsGroundedByCollider())
            {
                MakeInvisibleAndStartCountdown();
                state = TrapState.None;
            }
        }
    }

    private void OnTriggerEnter(Collider other) => TryTrap(other);
    private void OnCollisionEnter(Collision collision) => TryTrap(collision.collider);

    private void TryTrap(Collider other)
    {
        if (IsInvisible) return;
        if (state != TrapState.None) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        if (!other.CompareTag(trapTag)) return;

        TrapHazard trap = other.GetComponentInParent<TrapHazard>();
        if (trap == null) return;

        lastHitTime = Time.time;

        // 1) hemen zıplat (Y velocity set)
        Vector3 v = rb.linearVelocity;
        v.y = trap.BounceUpForce;
        rb.linearVelocity = v;

        // 2) inputları kapat (respawn’a kadar)
        LockInputs();

        // 3) ŞİMDİ: önce yerden kesilmeyi bekle, sonra yere basınca görünmez yap
        state = TrapState.WaitingLeaveGround;
    }

    private void MakeInvisibleAndStartCountdown()
    {
        IsInvisible = true;
        IsTrapped = true;                 // yere bastıktan sonra aktif olsun
        InvisibleStartTime = Time.time;   // ✅ geri sayım burada başlar

        if (visualRoot != null)
            visualRoot.SetActive(false);
    }

    // Collider bounds ile sağlam ground check
    private bool IsGroundedByCollider()
    {
        if (playerCollider == null) return false;

        Vector3 origin = playerCollider.bounds.center;
        float distance = playerCollider.bounds.extents.y + groundCheckExtra;

        return Physics.Raycast(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void LockInputs()
    {
        DisableAction(move);
        DisableAction(jump);
        DisableAction(sprint);
    }

    private void UnlockInputs()
    {
        EnableAction(move);
        EnableAction(jump);
        EnableAction(sprint);
    }

    private void DisableAction(InputActionReference ar)
    {
        if (ar?.action == null) return;
        if (ar.action.enabled) ar.action.Disable();
    }

    private void EnableAction(InputActionReference ar)
    {
        if (ar?.action == null) return;
        if (!ar.action.enabled) ar.action.Enable();
    }

    // SpawnPointRespawn bunu çağıracak
    public void RespawnFinished()
    {
        if (visualRoot != null)
            visualRoot.SetActive(true);

        UnlockInputs();

        IsTrapped = false;
        IsInvisible = false;
        InvisibleStartTime = -1f;
        state = TrapState.None;
    }
}
