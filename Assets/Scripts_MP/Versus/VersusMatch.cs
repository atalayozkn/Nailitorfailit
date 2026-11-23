using System;
using System.Collections.Generic;
using Flammables;
using Mirror;
using PlayerScripts;
using UnityEngine;

// Referee of the 2-player versus mode. One per versus scene.
// The server (host) owns the truth: who plays which side, build percent, wallets, hazards and the winner.
// Everything travels as plain Mirror messages. Without a network session it runs locally with one player on side A,
// so the scene can also be opened in normal play mode for a quick check.
public partial class VersusMatch : MonoBehaviour
{
    public const int NoWinner = -1;
    public const int Draw = 2;

    public static VersusMatch Instance { get; private set; }

    [Serializable]
    public class Side
    {
        public string label = "Player";
        public Color color = Color.white;
        [Tooltip("Root of this player's house. Its RoomControllers give the build percent.")]
        public Transform buildingRoot;
        [Tooltip("Root of this player's station row (cutter, mixer, depots).")]
        public Transform stationsRoot;
        [Tooltip("The depot area this player's dog guards.")]
        public BoxCollider depotZone;
        [Tooltip("Where this player's dog bed goes.")]
        public Transform dogAnchor;
        [Tooltip("Fire sockets of this house, same order on both sides.")]
        public ElectricalHazard_Prototype[] sockets;

        [NonSerialized] public uint playerNetId;
        [NonSerialized] public float percent;
        [NonSerialized] public int money;
        [NonSerialized] public RoomController[] rooms;
        [NonSerialized] public int totalTiles;
        [NonSerialized] public int paidRooms;
        [NonSerialized] public uint dogAppliedFor = uint.MaxValue;
    }

    [Serializable]
    public class DogSettings
    {
        [Tooltip("Dog loyalty (attitude percent) needed to guard the depot.")]
        public float guardLoyalty = 60f;
        [Tooltip("The dog reacts to a thief in the depot within this distance.")]
        public float guardRange = 14f;
        public float hitDistance = 1.5f;
        public float chargeTimeout = 6f;
        public float chargeCooldown = 6f;
        [Tooltip("A bait on the ground within this distance distracts the dog.")]
        public float baitRadius = 7f;
        [Tooltip("Seconds a fully loyal dog is busy with a bait.")]
        public float baitStallLoyal = 3f;
        [Tooltip("Seconds a dog with no loyalty is busy with a bait.")]
        public float baitStallDisloyal = 12f;
    }

    [SerializeField] private Side[] sides = { new Side(), new Side() };
    [SerializeField] private DogSettings dog = new DogSettings();
    public DogSettings Dog => dog;

    [Header("Money")]
    [SerializeField] private int startMoney = 150;
    [SerializeField] private int roomReward = 75;

    [Header("Fire (same socket sparks on both houses)")]
    [SerializeField] private float fireFirstDelay = 40f;
    [SerializeField] private Vector2 fireInterval = new Vector2(35f, 55f);

    [Header("Oil truck (network sessions; offline the car controller drives it)")]
    [Tooltip("Networked oil truck prefab.")]
    [SerializeField] private GameObject oilTruckPrefab;
    [SerializeField] private Transform[] lanes;
    [SerializeField] private float oilFirstDelay = 25f;
    [SerializeField] private Vector2 oilInterval = new Vector2(25f, 40f);
    [SerializeField] private float laneClearRadius = 10f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Hits")]
    [Tooltip("After a dog or bat stun the victim can not be stunned again for this long (stun itself lasts 3 s).")]
    [SerializeField] private float stunImmunity = 5f;

    [Header("Network")]
    [Tooltip("Versus-only networked prefabs (bat, bait, oil truck). Registered on every machine at scene start.")]
    [SerializeField] private GameObject[] networkPrefabs;

    private const float TickInterval = 0.25f;

    private readonly Dictionary<uint, float> lastStunAt = new Dictionary<uint, float>();
    private readonly List<FireHelper> fires = new List<FireHelper>();
    private bool[] wasBurning;
    private int winner = NoWinner;
    private bool serverHooked;
    private bool clientHooked;
    private float nextTickAt;
    private float nextFireAt;
    private float nextOilAt;
    private float nextFirePollAt;

    public int Winner => winner;
    public int SideCount => sides.Length;
    public Side GetSide(int index) => sides[index];

    private static bool Networked => NetworkModeHelper.IsNetworked;
    private static bool HasAuthority => NetworkServer.active || !Networked;

    #region LIFETIME

    private void Awake()
    {
        Instance = this;
        EnableSingleplayerFeel();
    }

    private void OnEnable()
    {
        CurrencyManager_MP.PerPlayerOverride = ServerWalletChange;
        Shop_MP.PerPlayerPurchase = ServerTrySpend;
    }

    private void OnDisable()
    {
        if (CurrencyManager_MP.PerPlayerOverride == ServerWalletChange) CurrencyManager_MP.PerPlayerOverride = null;
        if (Shop_MP.PerPlayerPurchase == ServerTrySpend) Shop_MP.PerPlayerPurchase = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DisableSingleplayerFeel();
    }

    private void Start()
    {
        foreach (Side side in sides)
        {
            side.rooms = side.buildingRoot != null
                ? side.buildingRoot.GetComponentsInChildren<RoomController>(true)
                : new RoomController[0];

            side.totalTiles = 0;
            foreach (RoomController room in side.rooms) side.totalTiles += room.GetMissingTileCount();

            if (side.buildingRoot != null) fires.AddRange(side.buildingRoot.GetComponentsInChildren<FireHelper>(true));
        }

        wasBurning = new bool[fires.Count];
        nextFireAt = Time.time + fireFirstDelay;
        nextOilAt = Time.time + oilFirstDelay;

        VersusHud.Create(this);
    }

    private void Update()
    {
        HookNetwork();

        if (HasAuthority)
        {
            if (Time.time >= nextTickAt)
            {
                nextTickAt = Time.time + TickInterval;
                ServerTick();
            }

            if (winner == NoWinner) HazardTick();
        }

        PollFires();
        LocalTick();
    }

    private void HookNetwork()
    {
        if (NetworkServer.active && !serverHooked)
        {
            serverHooked = true;
            NetworkServer.UnregisterHandler<VersusDogHitMessage>();
            NetworkServer.RegisterHandler<VersusDogHitMessage>(OnServerDogHit, false);
            NetworkServer.UnregisterHandler<VersusBatSwingMessage>();
            NetworkServer.RegisterHandler<VersusBatSwingMessage>(OnServerBatSwing, false);
            NetworkServer.UnregisterHandler<VersusFireStopMessage>();
            NetworkServer.RegisterHandler<VersusFireStopMessage>(OnServerFireStop, false);
            NetworkServer.UnregisterHandler<VersusRepairMessage>();
            NetworkServer.RegisterHandler<VersusRepairMessage>(OnServerRepair, false);
            NetworkServer.UnregisterHandler<VersusSlotMessage>();
            NetworkServer.RegisterHandler<VersusSlotMessage>(OnServerSlot, false);
        }

        if (NetworkClient.active && !clientHooked)
        {
            clientHooked = true;
            NetworkClient.UnregisterHandler<VersusStateMessage>();
            NetworkClient.RegisterHandler<VersusStateMessage>(ApplyState, false);
            NetworkClient.UnregisterHandler<VersusBatSwingMessage>();
            NetworkClient.RegisterHandler<VersusBatSwingMessage>(OnClientBatSwing, false);
            NetworkClient.UnregisterHandler<VersusFireMessage>();
            NetworkClient.RegisterHandler<VersusFireMessage>(ApplyFire, false);
            NetworkClient.UnregisterHandler<VersusFireStopMessage>();
            NetworkClient.RegisterHandler<VersusFireStopMessage>(ApplyFireStop, false);
            NetworkClient.UnregisterHandler<VersusRepairMessage>();
            NetworkClient.RegisterHandler<VersusRepairMessage>(ApplyRepair, false);
            NetworkClient.UnregisterHandler<VersusSlotMessage>();
            NetworkClient.RegisterHandler<VersusSlotMessage>(ApplySlot, false);
            RegisterPrefabs();
        }
    }

    private void RegisterPrefabs()
    {
        if (networkPrefabs == null) return;

        NetworkManager manager = NetworkManager.singleton;

        foreach (GameObject prefab in networkPrefabs)
        {
            if (prefab == null || !prefab.TryGetComponent(out NetworkIdentity identity)) continue;

            if (manager != null && !manager.spawnPrefabs.Contains(prefab)) manager.spawnPrefabs.Add(prefab);
            if (!NetworkClient.GetPrefab(identity.assetId, out _)) NetworkClient.RegisterPrefab(prefab);
        }
    }

    #endregion

    #region SIDES

    // Which side this machine's own player is on, -1 while unassigned (or a spectator).
    public int LocalSideIndex()
    {
        if (!Networked) return 0;
        if (NetworkClient.localPlayer == null) return -1;
        return SideIndexOf(NetworkClient.localPlayer.netId);
    }

    public int SideIndexOf(uint playerNetId)
    {
        if (playerNetId == 0) return -1;

        for (int i = 0; i < sides.Length; i++)
        {
            if (sides[i].playerNetId == playerNetId) return i;
        }

        return -1;
    }

    private void AssignPlayers()
    {
        if (!Networked) return;

        PlayerNetwork_MP[] players = FindObjectsByType<PlayerNetwork_MP>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Array.Sort(players, (a, b) => a.netId.CompareTo(b.netId));

        foreach (Side side in sides)
        {
            if (side.playerNetId == 0) continue;
            if (!Array.Exists(players, p => p.netId == side.playerNetId)) side.playerNetId = 0;
        }

        foreach (PlayerNetwork_MP player in players)
        {
            if (player.netId == 0 || SideIndexOf(player.netId) >= 0) continue;

            Side free = Array.Find(sides, s => s.playerNetId == 0);
            if (free == null) break;

            free.playerNetId = player.netId;
            free.money = startMoney;
        }
    }

    #endregion

    #region SERVER TICK

    private void ServerTick()
    {
        AssignPlayers();

        foreach (Side side in sides)
        {
            int missing = 0;
            int completedRooms = 0;

            foreach (RoomController room in side.rooms)
            {
                missing += Mathf.Max(0, room.GetMissingTileCount());
                if (room.IsCompleted()) completedRooms++;
            }

            side.percent = side.totalTiles > 0 ? Mathf.Clamp01(1f - (float)missing / side.totalTiles) : 0f;

            if (completedRooms > side.paidRooms)
            {
                int reward = roomReward * (completedRooms - side.paidRooms);
                side.paidRooms = completedRooms;

                if (Networked) side.money += reward;
                else CurrencyManager.Instance?.GainCurrency(reward);
            }
        }

        if (winner == NoWinner) CheckEnd();

        VersusStateMessage state = new VersusStateMessage
        {
            playerA = sides[0].playerNetId,
            playerB = sides[1].playerNetId,
            percentA = sides[0].percent,
            percentB = sides[1].percent,
            moneyA = sides[0].money,
            moneyB = sides[1].money,
            winner = winner
        };

        if (NetworkServer.active) NetworkServer.SendToAll(state, Channels.Reliable, true);
        else ApplyState(state);
    }

    // First to finish wins at once. When the round clock runs out the higher percent wins, equal is a draw.
    private void CheckEnd()
    {
        bool aDone = sides[0].percent >= 0.999f;
        bool bDone = sides[1].percent >= 0.999f;

        if (aDone || bDone)
        {
            EndMatch(aDone && bDone ? Draw : aDone ? 0 : 1);
            return;
        }

        GameTimeManager clock = GameTimeManager.Instance;
        if (clock == null || clock.GetCurrentTime() > 0f) return;

        float difference = sides[0].percent - sides[1].percent;
        EndMatch(Mathf.Abs(difference) < 0.0005f ? Draw : difference > 0f ? 0 : 1);
    }

    private void EndMatch(int result)
    {
        winner = result;
        GameTimeManager.Instance?.SetActivity(false);
        Debug.Log($"[VERSUS] match over, winner={(result == Draw ? "draw" : sides[result].label)} " +
                  $"({sides[0].percent:P0} vs {sides[1].percent:P0})");
    }

    #endregion

    #region CLIENT STATE

    private void ApplyState(VersusStateMessage state)
    {
        sides[0].playerNetId = state.playerA;
        sides[1].playerNetId = state.playerB;
        sides[0].percent = state.percentA;
        sides[1].percent = state.percentB;
        sides[0].money = state.moneyA;
        sides[1].money = state.moneyB;
        winner = state.winner;

        for (int i = 0; i < sides.Length; i++) ApplyDog(i);

        if (!Networked) return;

        int local = LocalSideIndex();
        CurrencyManager wallet = CurrencyManager.Instance;

        if (local >= 0 && wallet != null && wallet.GetCurrency() != sides[local].money)
        {
            wallet.SetCurrency(sides[local].money);
            wallet.RefreshLocalCurrencyText();
        }
    }

    // Moves the side owner's dog home to the depot and gives the dog its guard duty. Runs on every machine
    // so the dog package sits in the same place everywhere (DogSync_MP sends positions relative to it).
    private void ApplyDog(int index)
    {
        Side side = sides[index];
        if (side.dogAppliedFor == side.playerNetId) return;

        DogStateMachine dog = null;
        bool ownedHere = false;

        if (!Networked)
        {
            if (index != 0) { side.dogAppliedFor = side.playerNetId; return; }
            dog = FindAnyObjectByType<DogStateMachine>();
            ownedHere = true;
        }
        else if (side.playerNetId != 0 && NetworkClient.spawned.TryGetValue(side.playerNetId, out NetworkIdentity identity))
        {
            dog = identity.GetComponentInChildren<DogStateMachine>(true);
            ownedHere = identity.isOwned;
        }

        if (dog == null) return;

        side.dogAppliedFor = side.playerNetId;
        VersusDogGuard.Attach(dog, this, index, ownedHere);
    }

    #endregion

    #region WALLETS (SERVER)

    private Side SideOfPayer(NetworkIdentity payer)
    {
        uint id = payer != null ? payer.netId : NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0;
        int index = SideIndexOf(id);
        return index >= 0 ? sides[index] : null;
    }

    // Shop_MP asks this instead of the shared wallet.
    private bool ServerTrySpend(NetworkIdentity payer, int cost)
    {
        Side side = SideOfPayer(payer);
        if (side == null || side.money < cost) return false;

        side.money -= cost;
        return true;
    }

    // CurrencyManager_MP routes every other gain / spend here. conn is null for the host's own player.
    private bool ServerWalletChange(NetworkConnectionToClient conn, int delta)
    {
        Side side = SideOfPayer(conn != null ? conn.identity : null);
        if (side != null) side.money = Mathf.Max(0, side.money + delta);
        return true;
    }

    #endregion

    #region HITS

    public void ReportDogHit(uint victim)
    {
        if (!Networked) return;
        NetworkClient.Send(new VersusDogHitMessage { victim = victim });
    }

    private void OnServerDogHit(NetworkConnectionToClient conn, VersusDogHitMessage message)
    {
        if (conn.identity == null || conn.identity.netId == message.victim) return;
        ServerStun(message.victim);
    }

    private const float BatFlingSpeed = 7f;
    private const float BatFlingLift = 4f;

    // A bat hit throws the victim: the ragdoll takes a push away from the swinger first, then lies stunned (the same
    // as a car hit), instead of dropping on the spot.
    private bool ServerBatHit(NetworkIdentity swinger, uint victim)
    {
        if (winner != NoWinner) return false;
        if (!NetworkServer.spawned.TryGetValue(victim, out NetworkIdentity identity)) return false;

        PlayerNetwork_MP player = identity.GetComponentInChildren<PlayerNetwork_MP>(true);
        if (player == null || player.isDead) return false;

        if (lastStunAt.TryGetValue(victim, out float last) && Time.time - last < stunImmunity) return false;
        lastStunAt[victim] = Time.time;

        Vector3 away = player.transform.position - (swinger != null ? swinger.transform.position : player.transform.position);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = swinger != null ? swinger.transform.forward : Vector3.forward;
        away.Normalize();

        player.ServerStunFling(away * BatFlingSpeed + Vector3.up * BatFlingLift);
        return true;
    }

    private bool ServerStun(uint victim)
    {
        if (winner != NoWinner) return false;
        if (!NetworkServer.spawned.TryGetValue(victim, out NetworkIdentity identity)) return false;

        PlayerNetwork_MP player = identity.GetComponentInChildren<PlayerNetwork_MP>(true);
        if (player == null || player.isDead) return false;

        if (lastStunAt.TryGetValue(victim, out float last) && Time.time - last < stunImmunity) return false;

        lastStunAt[victim] = Time.time;
        player.RpcPlayHitReaction();
        return true;
    }

    public void ReportBatSwing(VersusBat bat, uint victim, bool hit)
    {
        if (!Networked)
        {
            bat.ApplySwing(hit, hit ? bat.Durability - 1 : bat.Durability);
            if (bat.Durability <= 0) bat.BreakOffline();
            return;
        }

        if (!bat.TryGetComponent(out NetworkIdentity identity)) return;
        NetworkClient.Send(new VersusBatSwingMessage { bat = identity.netId, victim = victim, hit = hit });
    }

    private void OnServerBatSwing(NetworkConnectionToClient conn, VersusBatSwingMessage message)
    {
        if (!NetworkServer.spawned.TryGetValue(message.bat, out NetworkIdentity identity)) return;
        if (!identity.TryGetComponent(out VersusBat bat) || bat.Durability <= 0) return;

        // Only a hit that really lands on the opponent costs the bat a use (a swing at a wall or at a player who was just
        // hit and is still protected costs nothing).
        bool landed = message.hit && message.victim != 0 && conn.identity != null && conn.identity.netId != message.victim &&
                      ServerBatHit(conn.identity, message.victim);
        message.hit = landed;

        message.remaining = message.hit ? bat.Durability - 1 : bat.Durability;
        NetworkServer.SendToAll(message, Channels.Reliable, true);

        if (message.remaining > 0) return;

        Transform holder = identity.transform.parent;
        NetworkIdentity holderIdentity = holder != null ? holder.GetComponentInParent<NetworkIdentity>() : null;
        if (identity.TryGetComponent(out CarriableObject_MP carriable)) carriable.ServerConsumeNetworked(holderIdentity, 0.3f);
    }

    private void OnClientBatSwing(VersusBatSwingMessage message)
    {
        if (!NetworkClient.spawned.TryGetValue(message.bat, out NetworkIdentity identity)) return;
        if (identity.TryGetComponent(out VersusBat bat)) bat.ApplySwing(message.hit, message.remaining);
    }

    #endregion

    #region HAZARDS

    private void HazardTick()
    {
        if (Time.time >= nextFireAt)
        {
            nextFireAt = Time.time + UnityEngine.Random.Range(fireInterval.x, fireInterval.y);

            int socketCount = sides[0].sockets != null ? sides[0].sockets.Length : 0;
            if (socketCount > 0)
            {
                VersusFireMessage fire = new VersusFireMessage { socketIndex = UnityEngine.Random.Range(0, socketCount) };
                if (NetworkServer.active) NetworkServer.SendToAll(fire, Channels.Reliable, true);
                else ApplyFire(fire);
            }
        }

        if (NetworkServer.active && oilTruckPrefab != null && lanes != null && lanes.Length > 0 && Time.time >= nextOilAt)
        {
            nextOilAt = Time.time + (ServerSpawnOilTruck() ? UnityEngine.Random.Range(oilInterval.x, oilInterval.y) : 0.5f);
        }
    }

    private void ApplyFire(VersusFireMessage message)
    {
        foreach (Side side in sides)
        {
            if (side.sockets == null || side.sockets.Length == 0) continue;

            ElectricalHazard_Prototype socket = side.sockets[message.socketIndex % side.sockets.Length];
            if (socket != null) socket.OnTrigger();
        }
    }

    // A fire that goes out on one machine (extinguisher, burnt out) goes out for everyone.
    private void PollFires()
    {
        if (!Networked || !NetworkClient.active || Time.time < nextFirePollAt) return;
        nextFirePollAt = Time.time + 0.3f;

        for (int i = 0; i < fires.Count; i++)
        {
            bool burning = fires[i] != null && fires[i].IsBurning();
            if (wasBurning[i] && !burning) NetworkClient.Send(new VersusFireStopMessage { fireIndex = i });
            wasBurning[i] = burning;
        }
    }

    private void OnServerFireStop(NetworkConnectionToClient conn, VersusFireStopMessage message)
    {
        NetworkServer.SendToAll(message, Channels.Reliable, true);
    }

    private void ApplyFireStop(VersusFireStopMessage message)
    {
        if (message.fireIndex < 0 || message.fireIndex >= fires.Count) return;

        FireHelper fire = fires[message.fireIndex];
        if (fire == null || !fire.IsBurning()) return;

        wasBurning[message.fireIndex] = false;

        IFlammable flammable = fire.GetComponentInParent<IFlammable>();
        if (flammable != null) flammable.OnFireStop();
        if (fire.IsBurning()) fire.StopFire();
    }

    private bool ServerSpawnOilTruck()
    {
        Transform lane = lanes[UnityEngine.Random.Range(0, lanes.Length)];
        if (lane == null) return false;

        foreach (Car_Active car in FindObjectsByType<Car_Active>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if ((car.transform.position - lane.position).sqrMagnitude < laneClearRadius * laneClearRadius) return false;
        }

        // Same idea as the singleplayer spawner: rest the wheels on the road whatever the prefab pivot is.
        Vector3 probe = lane.position + Vector3.up * 2f;
        bool hasGround = Physics.Raycast(probe, Vector3.down, out RaycastHit ground, 8f, groundMask, QueryTriggerInteraction.Ignore);

        GameObject truck = Instantiate(oilTruckPrefab, lane.position, lane.rotation);

        if (hasGround)
        {
            Physics.SyncTransforms();
            float bottom = float.MaxValue;
            foreach (Collider collider in truck.GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger) bottom = Mathf.Min(bottom, collider.bounds.min.y);
            }

            if (bottom < float.MaxValue) truck.transform.position += Vector3.up * (ground.point.y + 0.03f - bottom);
        }

        NetworkServer.Spawn(truck);
        return true;
    }

    #endregion
}
