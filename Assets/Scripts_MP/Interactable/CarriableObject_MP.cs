using System.Collections;
using ItemScript;
using Mirror;
using UnityEngine;

// Server-authoritative carriable. The server owns "who is holding this" (carrier) and refuses pickups of
// objects that are already held, placed in a station or consumed. Clients only ever apply what the server
// decided, so two players can no longer grab the same log and leave one of them stuck in a carrying state.
// Physics runs on every machine (no NetworkTransform), so the server also pushes the resting pose of
// free objects to clients; otherwise dropped items / cutter products end up in different places per machine.
public class CarriableObject_MP : NetworkBehaviour
{
    private const float SettleCheckInterval = 0.25f;
    private const float SettleSpeed = 0.08f;
    private const float SettleMinMove = 0.03f;

    private Rigidbody rb;
    private Collider col;
    // Every collider of the item (the fire extinguisher has several on child objects). Only the root collider used
    // to be switched off while carried, so the rest kept shoving the player (it walked backwards by itself).
    private Collider[] allColliders;
    private bool[] colliderWasEnabled;
    private CarriableObject_SP carriableSp;

    [SyncVar] private NetworkIdentity carrier;
    private bool serverConsumed;

    private Vector3 lastSentPos;
    private Quaternion lastSentRot;
    private float nextSettleCheck;

    public NetworkIdentity Carrier => carrier;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        allColliders = GetComponentsInChildren<Collider>(true);
        colliderWasEnabled = new bool[allColliders.Length];
        for (int i = 0; i < allColliders.Length; i++) colliderWasEnabled[i] = allColliders[i] != null && allColliders[i].enabled;
        carriableSp = GetComponent<CarriableObject_SP>();
    }

    // SP code that runs on one machine only can Instantiate an MP carriable without spawning it — the dog's
    // Aid ability (DogStateMachine.SpawnObject) runs on the dog owner's machine. Such a "ghost" (netId 0,
    // not a scene object) exists for that player alone and the server refuses every pickup of it.
    // Host: spawn it right away. Client: when the dog lets go of it, ask the server for a real copy.
    private bool localGhost;

    private void Start()
    {
        if (netId != 0 || netIdentity == null || netIdentity.sceneId != 0) return;

        if (NetworkServer.active)
        {
            NetworkServer.Spawn(gameObject);
            return;
        }

        if (NetworkClient.active) localGhost = true;
    }

    private void LateUpdate()
    {
        if (!localGhost || netId != 0) return;
        if (transform.parent != null || (carriableSp != null && carriableSp.IsOccupied())) return;

        localGhost = false;

        NetworkIdentity local = NetworkClient.localPlayer;
        PlayerScripts.PlayerNetwork_MP player = local != null ? local.GetComponentInChildren<PlayerScripts.PlayerNetwork_MP>(true) : null;
        if (player != null) player.RequestSpawnCarriable(name.Replace("(Clone)", "").Trim(), transform.position, transform.rotation);

        Destroy(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        lastSentPos = transform.position;
        lastSentRot = transform.rotation;
    }

    // Destroyed while someone holds it (consumed by a station, despawned, scene change...): without this the
    // holder keeps isCarrying=true / heavy movement and can never pick anything up again.
    public override void OnStopClient()
    {
        base.OnStopClient();
        ClearAnyHolder();
    }

    #region Client requests

    public void NotifyPickedUp(PlayerInteractionHandler interactor)
    {
        NetworkIdentity requester = interactor != null ? interactor.GetComponentInParent<NetworkIdentity>() : null;
        if (requester == null) return;

        if (isServer) ServerTryPickup(requester);
        else CmdRequestPickup(requester);
    }

    public void NotifyDropped(PlayerInteractionHandler interactor)
    {
        NetworkIdentity requester = interactor != null ? interactor.GetComponentInParent<NetworkIdentity>() : null;
        if (requester == null) return;

        if (isServer) ServerTryDrop(requester);
        else CmdRequestDrop(requester);
    }

    public void NotifyConsumed(float delay)
    {
        if (isServer) StartCoroutine(ServerDestroyAfterDelay(delay));
        else CmdRequestConsume(delay);
    }

    public void RequestConsume(NetworkIdentity requester)
    {
        if (isServer) ServerConsumeNetworked(requester);
        else CmdRequestConsumeNetworked(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestPickup(NetworkIdentity requester, NetworkConnectionToClient sender = null)
    {
        if (!SenderOwns(requester, sender)) return;
        ServerTryPickup(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestDrop(NetworkIdentity requester, NetworkConnectionToClient sender = null)
    {
        if (!SenderOwns(requester, sender)) return;
        ServerTryDrop(requester);
    }

    // Only the current holder may consume it (otherwise any client could delete any item).
    [Command(requiresAuthority = false)]
    private void CmdRequestConsume(float delay, NetworkConnectionToClient sender = null)
    {
        if (sender != null && carrier != sender.identity) return;
        StartCoroutine(ServerDestroyAfterDelay(delay));
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestConsumeNetworked(NetworkIdentity requester, NetworkConnectionToClient sender = null)
    {
        if (sender != null && carrier != sender.identity) return;
        ServerConsumeNetworked(sender != null ? sender.identity : requester);
    }

    // A client may only act for its own player object.
    private static bool SenderOwns(NetworkIdentity requester, NetworkConnectionToClient sender)
    {
        if (requester == null) return false;
        if (sender == null) return true;
        return requester.connectionToClient == sender;
    }

    #endregion

    #region Server decisions

    [Server]
    private void ServerTryPickup(NetworkIdentity requester)
    {
        string refusal = null;

        if (serverConsumed) refusal = "consumed";
        else if (carriableSp != null && carriableSp.IsOccupied()) refusal = "occupied (station/dog)";
        else if (carrier != null && carrier != requester) refusal = $"held by '{carrier.name}'";

        if (refusal != null)
        {
            InteractionDebug.Log($"Carriable '{name}': pickup by '{requester.name}' refused - {refusal}.");
            if (requester.connectionToClient != null) TargetPickupRefused(requester.connectionToClient);
            return;
        }

        // The requester may still hold another item on the server. With a free inventory slot every machine stows
        // it when this pickup is applied (RegisterCarriedObject); only a full inventory swaps, i.e. drops it first.
        PlayerInteractionHandler handler = HandlerOf(requester);
        CarriableObject_SP held = handler != null ? handler.GetCurrentCarriable() : null;
        if (held != null && held != carriableSp && handler.PickupWouldSwap() &&
            held.TryGetComponent(out CarriableObject_MP heldMp) && heldMp.carrier == requester)
        {
            heldMp.ServerTryDrop(requester);
        }

        carrier = requester;
        RpcApplyPickup(requester);
    }

    [Server]
    private void ServerTryDrop(NetworkIdentity requester)
    {
        if (carrier != requester)
        {
            // The client believes it holds this but the server disagrees: just clear its stale state.
            InteractionDebug.Log($"Carriable '{name}': drop by '{requester.name}' ignored - carrier is " +
                                 $"'{(carrier != null ? carrier.name : "nobody")}'.");
            RpcClearHolder(requester);
            return;
        }

        carrier = null;
        RpcApplyDrop(requester);
        nextSettleCheck = Time.time + SettleCheckInterval;
    }

    private static readonly System.Reflection.FieldInfo ThrowMultiplierField =
        typeof(CarriableObject_SP).GetField("throwMultiplier",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    // Throw emote: same force as CarriableObject_SP.OnThrow(1f), applied on every machine.
    [Server]
    public void ServerThrow(NetworkIdentity holder, Vector3 direction)
    {
        if (carrier == null || carrier != holder) return;

        carrier = null;

        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude < 0.0001f) flat = holder.transform.forward;

        float force = 1f;
        if (carriableSp != null && ThrowMultiplierField?.GetValue(carriableSp) is float multiplier) force *= multiplier;

        RpcApplyThrow(holder, flat.normalized * force);
        nextSettleCheck = Time.time + 1f;

        thrower = holder;
        thrownAt = Time.time;
        thrownUntil = Time.time + ThrowHitWindow;
    }

    // A thrown item that reaches another player knocks them over (ragdoll + a push along the item's flight).
    private const float ThrowHitWindow = 2.5f;
    private const float ThrowHitRadius = 0.7f;
    private const float ThrowMinSpeed = 2f;
    private const float ThrowerGrace = 0.35f;
    private NetworkIdentity thrower;
    private float thrownAt;
    private float thrownUntil;

    [ServerCallback]
    private void FixedUpdate()
    {
        if (Time.time > thrownUntil || rb == null || rb.isKinematic) return;
        if (rb.linearVelocity.sqrMagnitude < ThrowMinSpeed * ThrowMinSpeed) return;

        foreach (Collider hit in Physics.OverlapSphere(transform.position, ThrowHitRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            PlayerScripts.PlayerNetwork_MP player = hit.GetComponentInParent<PlayerScripts.PlayerNetwork_MP>();
            if (player == null || player.isDead) continue;

            // The thrower is not hit by their own throw while it leaves their hands.
            if (player.netIdentity == thrower && Time.time - thrownAt < ThrowerGrace) continue;

            Vector3 flight = rb.linearVelocity;
            flight.y = 0f;
            Vector3 push = (flight.sqrMagnitude > 0.01f ? flight.normalized : transform.forward) * 5f + Vector3.up * 3.5f;

            InteractionDebug.Log($"[MP-THROW] '{name}' hit '{player.name}'.");
            player.ServerStunFling(push);

            thrownUntil = 0f; // one hit per throw
            return;
        }
    }

    [ClientRpc]
    private void RpcApplyThrow(NetworkIdentity holder, Vector3 impulse)
    {
        ApplyDropLocally(holder);
        if (rb != null) rb.AddForce(impulse, ForceMode.Impulse);
    }

    // Used when the holder dies / is removed: drop it where it is, for everyone.
    [Server]
    public void ServerForceDrop(NetworkIdentity holder)
    {
        if (carrier == null || carrier != holder) return;
        ServerTryDrop(holder);
    }

    [Server]
    public void ServerGiveTo(NetworkIdentity requester)
    {
        if (requester == null) return;
        carrier = requester;
        RpcApplyPickup(requester);
    }

    [Server]
    public void ServerPlaceInStation(NetworkIdentity station, NetworkIdentity requester, bool hideVisuals)
    {
        InteractionDebug.Log($"Carriable '{name}': placed into station " +
                             $"'{(station != null ? station.name : "NULL")}' for {requester?.name ?? "host"}");

        carrier = null;
        RpcApplyPlaceInStation(station, requester, hideVisuals);
    }

    [Server]
    public void ServerConsumeNetworked(NetworkIdentity requester, float destroyDelay = 0f)
    {
        if (serverConsumed) return;

        InteractionDebug.Log($"Carriable '{name}': consumed (networked) for {requester?.name ?? "host"}");

        serverConsumed = true;
        NetworkIdentity holder = carrier;
        carrier = null;

        if (carriableSp != null) carriableSp.MarkConsumedForNetwork();

        RpcApplyConsumed(requester);
        if (holder != null && holder != requester) RpcClearHolder(holder);

        if (destroyDelay > 0f) StartCoroutine(ServerDestroyAfterDelay(destroyDelay));
        else NetworkServer.Destroy(gameObject);
    }

    [Server]
    public void ServerDropFromStation()
    {
        carrier = null;
        RpcApplyDropFromStation();
        nextSettleCheck = Time.time + SettleCheckInterval;
    }

    [Server]
    private IEnumerator ServerDestroyAfterDelay(float delay)
    {
        serverConsumed = true;
        yield return new WaitForSeconds(delay);

        if (gameObject != null) NetworkServer.Destroy(gameObject);
    }

    // Resting-pose sync for free objects (dropped, thrown, spawned by a station, pushed).
    [ServerCallback]
    private void Update()
    {
        if (Time.time < nextSettleCheck) return;
        nextSettleCheck = Time.time + SettleCheckInterval;

        if (carrier != null || serverConsumed || rb == null || rb.isKinematic) return;
        if (transform.parent != null) return;
        if (carriableSp != null && carriableSp.IsOccupied()) return;
        if (!rb.IsSleeping() && rb.linearVelocity.sqrMagnitude > SettleSpeed * SettleSpeed) return;

        if ((transform.position - lastSentPos).sqrMagnitude < SettleMinMove * SettleMinMove &&
            Quaternion.Angle(transform.rotation, lastSentRot) < 2f) return;

        lastSentPos = transform.position;
        lastSentRot = transform.rotation;
        RpcSettle(lastSentPos, lastSentRot);
    }

    #endregion

    #region Client application

    [ClientRpc]
    private void RpcApplyPickup(NetworkIdentity carrierIdentity) => ApplyPickupLocally(carrierIdentity);

    [ClientRpc]
    private void RpcApplyDrop(NetworkIdentity carrierIdentity) => ApplyDropLocally(carrierIdentity);

    [ClientRpc]
    private void RpcClearHolder(NetworkIdentity holder) => ClearHandlerIfHolding(holder);

    [TargetRpc]
    private void TargetPickupRefused(NetworkConnectionToClient target)
    {
        InteractionDebug.Log($"Carriable '{name}': server refused the pickup (someone else has it).");
    }

    [ClientRpc]
    private void RpcSettle(Vector3 position, Quaternion rotation)
    {
        if (isServer) return;
        if (transform.parent != null) return;

        transform.SetPositionAndRotation(position, rotation);

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    [ClientRpc]
    private void RpcApplyPlaceInStation(NetworkIdentity station, NetworkIdentity requester, bool hideVisuals)
    {
        Transform placementTransform = station != null ? station.transform : null;

        if (station != null)
        {
            Transform configured = MpSharedCompat.GetPlacementTransform(station);
            if (configured != null) placementTransform = configured;
        }

        if (placementTransform != null)
        {
            transform.SetParent(placementTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        SetItemColliders(false);
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.Sleep();
        }

        if (carriableSp != null)
        {
            carriableSp.SetOccupied();
            carriableSp.SetVisualsSafe(!hideVisuals);
        }

        ClearHandlerIfHolding(requester);

        if (!isServer) MpSharedCompat.OnLoadedIntoStationClient(station);
    }

    [ClientRpc]
    private void RpcApplyConsumed(NetworkIdentity requester)
    {
        if (!isServer && carriableSp != null) carriableSp.MarkConsumedForNetwork();

        ClearHandlerIfHolding(requester);
    }

    [ClientRpc]
    private void RpcApplyDropFromStation()
    {
        if (rb != null)
        {
            rb.WakeUp();
            rb.isKinematic = false;
        }

        transform.SetParent(null);

        SetItemColliders(true);

        if (carriableSp != null) carriableSp.SetVisualsSafe(true);
    }

    // false: all colliders off (carried, in a station). true: back to what the prefab had enabled.
    private void SetItemColliders(bool enabled)
    {
        if (allColliders == null) return;

        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider c = allColliders[i];
            if (c == null) continue;

            c.enabled = enabled && colliderWasEnabled[i];
        }
    }

    private void ApplyPickupLocally(NetworkIdentity carrierIdentity)
    {
        PlayerInteractionHandler carrierInteraction = HandlerOf(carrierIdentity);
        if (carrierInteraction == null) return;

        // Another player's copy may still think it holds this (lost race on an older build): release it.
        foreach (PlayerInteractionHandler other in FindObjectsByType<PlayerInteractionHandler>(FindObjectsSortMode.None))
        {
            if (other != carrierInteraction && other.GetCurrentCarriable() == carriableSp) other.ClearCarriedObject();
        }

        SetItemColliders(false);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.Sleep();
        }

        transform.SetParent(carrierInteraction.GetCarryTransform());
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (carriableSp != null) carrierInteraction.RegisterCarriedObject(carriableSp);
    }

    private void ApplyDropLocally(NetworkIdentity carrierIdentity)
    {
        transform.SetParent(null);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        SetItemColliders(true);

        // A stowed item is hidden. The SP death state may already have let go of it locally (DropAllCarried drops
        // the inventory without un-hiding), so show it again here whatever the handler thinks.
        if (carriableSp != null) carriableSp.SetStowed(false);

        ReleaseFromHandler(carrierIdentity);
    }

    // The item left its carrier. The item in hand clears the hand; a stowed one frees its slot and shows again.
    private void ReleaseFromHandler(NetworkIdentity holder)
    {
        PlayerInteractionHandler handler = HandlerOf(holder);
        if (handler == null) return;

        CarriableObject_SP held = handler.GetCurrentCarriable();
        if (held != carriableSp && carriableSp != null) handler.ReleaseCarried(carriableSp);

        // "held == null" also covers an already destroyed object (Unity fake-null) whose flags are still set.
        if (held == null || held == carriableSp) handler.ClearCarriedObject();
    }

    private void ClearHandlerIfHolding(NetworkIdentity holder)
    {
        PlayerInteractionHandler handler = HandlerOf(holder);
        if (handler == null) return;

        CarriableObject_SP held = handler.GetCurrentCarriable();

        // "held == null" also covers an already destroyed object (Unity fake-null) whose flags are still set.
        if (held == null || held == carriableSp) handler.ClearCarriedObject();
    }

    private void ClearAnyHolder()
    {
        if (carriableSp == null) return;

        foreach (PlayerInteractionHandler handler in FindObjectsByType<PlayerInteractionHandler>(FindObjectsSortMode.None))
        {
            if (handler != null && handler.GetCurrentCarriable() == carriableSp) handler.ClearCarriedObject();
        }
    }

    private static PlayerInteractionHandler HandlerOf(NetworkIdentity identity)
    {
        if (identity == null) return null;

        PlayerInteractionHandler handler = identity.GetComponentInChildren<PlayerInteractionHandler>(true);
        return handler != null ? handler : identity.GetComponentInParent<PlayerInteractionHandler>();
    }

    #endregion
}
