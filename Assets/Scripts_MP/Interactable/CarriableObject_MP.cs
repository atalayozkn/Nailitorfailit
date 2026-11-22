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
    // After a drop / throw / station release the resting pose is re-sent for a few seconds, even when it equals the last
    // one sent: every machine simulates the fall on its own, so their copies rest in slightly different places, and the
    // item has to be where the server has it for the pickup to be reliable.
    private const float ForcedSettleSeconds = 6f;
    private float forceSettleUntil;
    private void ForceSettleWindow() => forceSettleUntil = Time.time + ForcedSettleSeconds;

    public NetworkIdentity Carrier => carrier;

    // Uses left in a limited tool (the fire extinguisher), shared through the server so the count follows the item
    // from hand to hand and is not refilled on the machines that did not use it. -1 = untouched.
    [SyncVar(hook = nameof(OnFuelChanged))] private int netFuel = -1;

    // The holder started spraying (fire extinguisher): everybody else plays the jet too.
    public void ReportSpray()
    {
        if (isServer) RpcSpray();
        else if (NetworkClient.active) CmdSpray();
    }

    [Command(requiresAuthority = false)]
    private void CmdSpray(NetworkConnectionToClient sender = null)
    {
        if (sender != null && carrier != sender.identity) return;
        RpcSpray();
    }

    [ClientRpc]
    private void RpcSpray()
    {
        // The user already plays it themselves.
        if (carrier != null && carrier.isOwned) return;

        if (TryGetComponent(out FireExtinguisher extinguisher)) extinguisher.PlayRemoteSpray();
    }

    public void ReportFuel(int fuel)
    {
        if (isServer) ServerSetFuel(fuel);
        else if (NetworkClient.active) CmdReportFuel(fuel);
    }

    [Command(requiresAuthority = false)]
    private void CmdReportFuel(int fuel, NetworkConnectionToClient sender = null)
    {
        if (sender != null && carrier != sender.identity) return;
        ServerSetFuel(fuel);
    }

    [Server]
    private void ServerSetFuel(int fuel) => netFuel = netFuel < 0 ? fuel : Mathf.Min(netFuel, fuel);

    private void OnFuelChanged(int oldValue, int newValue)
    {
        if (newValue >= 0 && TryGetComponent(out FireExtinguisher extinguisher)) extinguisher.ApplyNetworkedFuel(newValue);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (netFuel >= 0) OnFuelChanged(-1, netFuel);
    }

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
        Reconcile();

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

        // "Occupied" means in a station or in the dog's mouth, and both of those are a parent. A free item that still
        // has the flag (a hand-over that missed the reset) would be refused for ever: clear it.
        if (carriableSp != null && carriableSp.IsOccupied() && transform.parent == null)
        {
            ClearOccupied();
            InteractionDebug.Log($"[MP-HEAL] '{name}' #{netId}: stale 'occupied' flag cleared on pickup.");
        }

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
        ForceSettleWindow();
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
        ForceSettleWindow();

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
        if (rb == null) return;

        rb.AddForce(impulse, ForceMode.Impulse);

        // SP's OnThrow also lifts the item (throwUpwardSpeed): without it the thrown item dropped almost at the feet.
        if (ThrowUpwardField?.GetValue(carriableSp) is float upward && upward > 0f)
            rb.AddForce(Vector3.up * upward, ForceMode.VelocityChange);
    }

    private static readonly System.Reflection.FieldInfo ThrowUpwardField =
        typeof(CarriableObject_SP).GetField("throwUpwardSpeed",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

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
        ForceSettleWindow();
    }

    // Same as ServerDropFromStation, but the item leaves the station at a given point with a given push.
    [Server]
    public void ServerDropFromStationAt(Vector3 position, Vector3 velocity)
    {
        carrier = null;
        RpcApplyDropFromStationAt(position, velocity);
        nextSettleCheck = Time.time + SettleCheckInterval;
        ForceSettleWindow();
    }

    [ClientRpc]
    private void RpcApplyDropFromStationAt(Vector3 position, Vector3 velocity)
    {
        transform.SetParent(null);
        transform.position = position;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.position = position;
            rb.linearVelocity = velocity;
            rb.WakeUp();
        }

        SetItemColliders(true);

        if (carriableSp != null)
        {
            carriableSp.SetVisualsSafe(true);
            ClearOccupied();
        }
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
        nextSettleCheck = Time.time + (Time.time < forceSettleUntil ? 0.75f : SettleCheckInterval);

        if (carrier != null || serverConsumed || rb == null || rb.isKinematic) return;
        if (transform.parent != null) return;
        if (carriableSp != null && carriableSp.IsOccupied()) return;
        if (!rb.IsSleeping() && rb.linearVelocity.sqrMagnitude > SettleSpeed * SettleSpeed) return;

        bool forced = Time.time < forceSettleUntil;
        if (!forced && (transform.position - lastSentPos).sqrMagnitude < SettleMinMove * SettleMinMove &&
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

        // Code first, picture second: a resting pose is only drawn for an item that, by the synced state, is free. The
        // carrier SyncVar can arrive a moment before the pickup RPC, and a settle that lands in that gap snapped the item
        // to where the server had it (next to whoever threw it) just as its new holder picked it up.
        if (carrier != null || serverConsumed) return;
        if (carriableSp != null && (carriableSp.IsOccupied() || HeldByAnyHandler(carriableSp))) return;

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
        // Code first: the hand lets go before the picture moves (a failure here must not leave the item in the station
        // while the handler still says it is carried).
        try
        {
            ClearHandlerIfHolding(requester);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MP] Carriable '{name}': clearing the hand for the station failed ({e.GetType().Name}: {e.Message}).");
        }

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

        if (carriableSp != null)
        {
            carriableSp.SetVisualsSafe(true);
            ClearOccupied();
        }
    }

    private static readonly System.Reflection.FieldInfo OccupiedField =
        typeof(CarriableObject_SP).GetField("isOccupied", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    // An item that went into a station is "occupied" (nobody may pick it up). When the station gives it back (failed
    // mix, product) it has to be free again, or the server refuses every pickup of it.
    private void ClearOccupied()
    {
        if (carriableSp != null && OccupiedField != null) OccupiedField.SetValue(carriableSp, false);
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

        // The item-vs-item copies follow their source colliders (see CarriableContact).
        CarriableContact contact = GetComponentInChildren<CarriableContact>(true);
        if (contact != null) contact.Sync();
    }

    // ---- Self-healing visuals -------------------------------------------------------------------------------------
    // The server's `carrier` SyncVar is the truth. What each machine shows (parent = hand, handler slot) normally
    // follows through the pickup/drop RPCs, but any lost, reordered or half-applied step (carrier not spawned yet when the
    // RPC arrived, a local SP path that let go on its own...) used to stay wrong for good: the plank looked taken but was
    // not, or the other way round. So every machine compares what it shows with the server's carrier a few times a
    // second and re-applies the server state when the difference lasts (RPCs still in flight do not count).
    private const float ReconcileInterval = 0.25f;
    private const int ReconcileConfirmSamples = 3;
    // How often the self-heal had to step in this session (shown in the F1 analysis panel).
    public static int HealCount;
    private float nextReconcileAt = -1f;
    private int mismatchSamples;

    private void Reconcile()
    {
        if (!NetworkClient.active || netId == 0 || carriableSp == null) return;
        if (Time.unscaledTime < nextReconcileAt) return;
        nextReconcileAt = Time.unscaledTime + ReconcileInterval + (netId % 5) * 0.01f;

        // In a station, in the dog's mouth, being thrown/consumed: other systems own those states.
        if (carriableSp.IsOccupied() || serverConsumed || LocallyConsumed()) { mismatchSamples = 0; return; }

        NetworkIdentity serverCarrier = carrier;
        bool wrong;

        if (serverCarrier != null)
        {
            PlayerInteractionHandler holder = HandlerOf(serverCarrier);
            if (holder == null || holder.GetCarryTransform() == null) { mismatchSamples = 0; return; } // carrier not spawned here yet

            // An emote (throw) deliberately moves the item to the hand bone. Re-applying the pickup would register the
            // item again, and registering forces the player back to Idle: that cut the throw emote short.
            PlayerStateMachine holderState = holder.GetComponentInParent<PlayerStateMachine>(true);
            if (holderState != null && holderState.currentPlayerState == PlayerStates.Emote) { mismatchSamples = 0; return; }

            wrong = transform.parent != holder.GetCarryTransform() || !HandlerHolds(holder, carriableSp);
        }
        else
        {
            // Free item: not in any hand and in no handler. (A parent that is not a hand = a station, left alone.)
            wrong = HeldByAnyHandler(carriableSp) || IsInAnyHand(transform.parent);
        }

        if (!wrong) { mismatchSamples = 0; return; }
        if (++mismatchSamples < ReconcileConfirmSamples) return;
        mismatchSamples = 0;

        HealCount++;
        InteractionDebug.Log($"[MP-HEAL] '{name}' #{netId}: showed the wrong holder, server carrier is " +
                             $"'{(serverCarrier != null ? serverCarrier.name : "nobody")}' - state re-applied.");

        if (serverCarrier != null)
        {
            ApplyPickupLocally(serverCarrier);
        }
        else
        {
            ClearAnyHolder();
            ApplyDropLocally(null);
        }
    }

    private static readonly System.Reflection.FieldInfo ConsumedField =
        typeof(CarriableObject_SP).GetField("isConsumed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    // Used up here already (drunk, built in): it is on its way out, nothing to put in a hand.
    private bool LocallyConsumed() => ConsumedField != null && ConsumedField.GetValue(carriableSp) is bool consumed && consumed;

    private static bool HandlerHolds(PlayerInteractionHandler handler, CarriableObject_SP item)
    {
        if (handler.GetCurrentCarriable() == item) return true;

        for (int i = 0; i < PlayerInteractionHandler.InventorySize; i++)
            if (handler.GetSlotItem(i) == item) return true;

        return false;
    }

    // One scene search per second shared by every item (there can be dozens of items).
    private static PlayerInteractionHandler[] cachedHandlers = new PlayerInteractionHandler[0];
    private static float cachedHandlersAt = -10f;

    private static PlayerInteractionHandler[] Handlers()
    {
        if (Time.unscaledTime - cachedHandlersAt > 1f)
        {
            cachedHandlersAt = Time.unscaledTime;
            cachedHandlers = FindObjectsByType<PlayerInteractionHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        return cachedHandlers;
    }

    private static bool HeldByAnyHandler(CarriableObject_SP item)
    {
        foreach (PlayerInteractionHandler handler in Handlers())
            if (handler != null && HandlerHolds(handler, item)) return true;

        return false;
    }

    private static bool IsInAnyHand(Transform parent)
    {
        if (parent == null) return false;

        foreach (PlayerInteractionHandler handler in Handlers())
            if (handler != null && handler.GetCarryTransform() == parent) return true;

        return false;
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

        // Code first, visual second. The handler's state ("I carry this") is what every later action reads; the visual
        // is only its picture. If the code step fails, nothing is shown: a hand that looks full but cannot drop is the
        // worst outcome, and Reconcile retries from the server's carrier.
        if (carriableSp != null)
        {
            try
            {
                carrierInteraction.RegisterCarriedObject(carriableSp);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MP] Carriable '{name}': registering with '{carrierInteraction.name}' failed ({e.GetType().Name}: {e.Message}); the visual is not applied, it is retried from the server's carrier.");
                return;
            }

            // Check the code state before drawing it: the handler must really hold this item in hand now.
            if (carrierInteraction.GetCurrentCarriable() != carriableSp)
            {
                Debug.LogWarning($"[MP] Carriable '{name}': '{carrierInteraction.name}' does not have it in hand after registering; the visual is not applied, it is retried from the server's carrier.");
                return;
            }
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
    }

    private void ApplyDropLocally(NetworkIdentity carrierIdentity)
    {
        // Code first (the hand lets go), then the picture (the item falls). The visual is applied even if the code
        // step throws: an item that stays glued to a hand it no longer belongs to is the visible bug.
        try
        {
            ReleaseFromHandler(carrierIdentity);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MP] Carriable '{name}': releasing from the handler failed ({e.GetType().Name}: {e.Message}).");
        }

        // Check the code state: the hand must really have let go. If not, clear it by force, then show the item falling.
        PlayerInteractionHandler releasedHandler = HandlerOf(carrierIdentity);
        if (releasedHandler != null && carriableSp != null && releasedHandler.GetCurrentCarriable() == carriableSp)
            releasedHandler.ClearCarriedObject();

        transform.SetParent(null);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        SetItemColliders(true);

        // Leaves from the holder's hand: keep it off the holder's own capsule and limbs until it is clear,
        // or a throw can hit the thrower and lose part of its impulse.
        Component holder = releasedHandler != null ? releasedHandler : carrierIdentity;
        if (holder != null) ThrowReleaseGuard.Protect(gameObject, holder);

        // A stowed item is hidden. The SP death state may already have let go of it locally (DropAllCarried drops
        // the inventory without un-hiding), so show it again here whatever the handler thinks.
        if (carriableSp != null) carriableSp.SetStowed(false);
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
