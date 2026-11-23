using System.Collections;
using ItemScript;
using PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;

// Shop item for the versus mode: a baseball bat. Swing it with the Use key while holding it.
// Hitting the other player stuns them like a car hit (their item flies out of their hands).
// Every hit costs durability, also on walls, trees and other large objects. Dogs can not be hit.
// Sits on a normal carriable, so buying, carrying and dropping all use the existing code.
public class VersusBat : MonoBehaviour
{
    [SerializeField] private CarriableObject_SP carriable;
    [SerializeField] private InputActionReference useAction;
    [Tooltip("Part that swings. Empty = the whole object.")]
    [SerializeField] private Transform visual;

    [Header("Settings")]
    [SerializeField] private int maxHits = 2; // two hits on opponents
    [SerializeField] private float swingCooldown = 0.9f;
    [Tooltip("The player plays the same hammering animation as nailing a tile; the hit lands this long after it starts.")]
    [SerializeField] private float hitDelay = 0.25f;
    [SerializeField] private float animationSeconds = 0.55f;
    [SerializeField] private Vector3 halfExtents = new Vector3(0.7f, 0.7f, 0.8f);
    [SerializeField] private float forwardOffset = 0.9f;
    [Tooltip("Height of the hit box bottom above the holder's feet, so the floor never counts as a hit.")]
    [SerializeField] private float floorClearance = 0.35f;
    [Tooltip("Objects at least this big (bounds diagonal, metres) cost durability when hit.")]
    [SerializeField] private float largeObjectSize = 1.5f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private LayerMask ignoredLayers;

    private const float ReachMeters = 2.2f;

    private readonly Collider[] buffer = new Collider[96];
    private float nextSwingAt;
    private Coroutine swingRoutine;
    private Quaternion restRotation = Quaternion.identity;

    public int Durability { get; private set; }

    private void Awake()
    {
        Durability = maxHits;
        if (carriable == null) carriable = GetComponent<CarriableObject_SP>();
        restRotation = (visual != null ? visual : transform).localRotation;
    }

    private void Update()
    {
        if (Durability <= 0 || useAction == null || Time.time < nextSwingAt) return;
        if (!useAction.action.WasPressedThisFrame()) return;

        PlayerInteractionHandler holder = LocalHolder();
        if (holder != null) StartCoroutine(SwingWithAnimation(holder));
    }

    // Other machines see the animation through the owner's synced animator state, no extra message needed.
    private IEnumerator SwingWithAnimation(PlayerInteractionHandler holder)
    {
        nextSwingAt = Time.time + swingCooldown;
        PlayHammerAnimation(holder);

        yield return new WaitForSeconds(hitDelay);

        if (holder != null && LocalHolder() == holder) Swing(holder);
    }

    private void PlayHammerAnimation(PlayerInteractionHandler holder)
    {
        if (!holder.TryGetComponent(out PlayerStateMachine state)) return;
        if (state.currentPlayerState != PlayerStates.Idle && state.currentPlayerState != PlayerStates.Navigation) return;

        holder.HoldInteractFor(animationSeconds);
        state.ChangeToInteractState();
    }

    // The player holding this bat, but only when that player is controlled on this machine and on their feet.
    private PlayerInteractionHandler LocalHolder()
    {
        PlayerInteractionHandler handler = GetComponentInParent<PlayerInteractionHandler>();
        if (handler == null || handler.GetCurrentCarriable() != carriable) return null;

        if (NetworkModeHelper.IsNetworked && (!handler.TryGetComponent(out PlayerNetwork_MP network) || !network.isLocalPlayer)) return null;

        if (handler.TryGetComponent(out PlayerStateMachine state) &&
            (state.currentPlayerState == PlayerStates.Stunned || state.currentPlayerState == PlayerStates.Dead)) return null;

        return handler;
    }

    private void Swing(PlayerInteractionHandler holder)
    {
        nextSwingAt = Time.time + swingCooldown;

        Transform body = holder.transform;
        Vector3 center = body.position + Vector3.up * (floorClearance + halfExtents.y) + body.forward * forwardOffset;
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, body.rotation, hitMask, QueryTriggerInteraction.Ignore);

        Transform ownRoot = body.root;
        uint victim = 0;
        float nearest = float.MaxValue;
        bool hit = false;

        // Players are found by position as well as by collider: a crowded hit box (floor tiles, props) or a collider on
        // another layer used to let a swing miss a player standing right in front.
        foreach (PlayerNetwork_MP candidate in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.isDead || candidate.transform.IsChildOf(ownRoot)) continue;

            Vector3 offset = candidate.transform.position - body.position;
            if (Mathf.Abs(offset.y) > 1.6f) continue;

            Vector3 flat = new Vector3(offset.x, 0f, offset.z);
            if (flat.magnitude > ReachMeters || Vector3.Angle(body.forward, flat) > 75f) continue;

            hit = true;
            float distance = flat.sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                victim = candidate.netId;
            }
        }

        for (int i = 0; i < count; i++)
        {
            Collider other = buffer[i];
            if (other.transform.IsChildOf(ownRoot) || other.transform.IsChildOf(transform)) continue;

            PlayerNetwork_MP player = other.GetComponentInParent<PlayerNetwork_MP>();
            if (player != null)
            {
                if (player.isDead) continue;

                hit = true;
                float distance = (player.transform.position - body.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    victim = player.netId;
                }
                continue;
            }

            if ((ignoredLayers.value & (1 << other.gameObject.layer)) != 0 || other is TerrainCollider) continue;
            if (other.GetComponentInParent<DogStateMachine>() != null) continue;
            if (other.GetComponentInParent<CarriableObject_SP>() != null) continue;

            // Walls, trees and other big objects no longer wear the bat out: only a player in front of it counts.
        }

        InteractionDebug.Log($"[BAT] swing: colliders={count} victim={(victim != 0 ? victim.ToString() : "none")} hit={hit} versusMatch={(VersusMatch.Instance != null)}");

        if (VersusMatch.Instance != null) VersusMatch.Instance.ReportBatSwing(this, victim, hit);
        else ApplySwing(hit, hit ? Durability - 1 : Durability);
    }

    // Runs on every machine: shows the swing and keeps the durability in step.
    public void ApplySwing(bool hit, int remaining)
    {
        Durability = remaining;

        if (swingRoutine != null) StopCoroutine(swingRoutine);
        if (isActiveAndEnabled) swingRoutine = StartCoroutine(SwingRoutine());

        if (hit && Durability <= 0) Debug.Log("[VERSUS] baseball bat broke.");
    }

    public void BreakOffline()
    {
        if (carriable != null) carriable.OnConsume();
        else Destroy(gameObject);
    }

    private IEnumerator SwingRoutine()
    {
        Transform target = visual != null ? visual : transform;
        Quaternion rest = restRotation;
        Quaternion wound = Quaternion.Euler(0f, 70f, 0f) * rest;
        Quaternion through = Quaternion.Euler(0f, -110f, 0f) * rest;

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
        {
            target.localRotation = Quaternion.Slerp(wound, through, t);
            yield return null;
        }

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
        {
            target.localRotation = Quaternion.Slerp(through, rest, t);
            yield return null;
        }

        target.localRotation = rest;
        swingRoutine = null;
    }
}
