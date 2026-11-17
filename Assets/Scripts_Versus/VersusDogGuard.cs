using System.Reflection;
using PlayerScripts;
using UnityEngine;
using UnityEngine.AI;

// Versus-only duties of a dog, added to the existing dog at runtime by VersusMatch (the dog prefab is untouched):
// - its home (bed, bowl, patrol area) moves to its master's depot,
// - a loyal dog rams an enemy who steps into the depot and stuns them,
// - a bait lying nearby pulls it away from guard duty; the less loyal the dog, the longer it stalls.
// The decisions run only on the machine of the dog's master, like the rest of the dog AI in multiplayer.
public class VersusDogGuard : MonoBehaviour
{
    private static readonly MethodInfo PatrolBoundariesSetter =
        typeof(DogStateMachine).GetProperty("patrolBoundaries", BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true);

    private DogStateMachine dog;
    private VersusMatch match;
    private VersusMatch.DogSettings settings;
    private int sideIndex;
    private bool ownedHere;
    private bool busy;
    private float nextScanAt;
    private float nextChargeAt;
    private float nextPlayerRefreshAt;
    private PlayerNetwork_MP[] players = new PlayerNetwork_MP[0];

    public VersusMatch.DogSettings Settings => settings;

    public static void Attach(DogStateMachine dog, VersusMatch match, int sideIndex, bool ownedHere)
    {
        if (!dog.TryGetComponent(out VersusDogGuard guard)) guard = dog.gameObject.AddComponent<VersusDogGuard>();

        guard.dog = dog;
        guard.match = match;
        guard.settings = match.Dog;
        guard.sideIndex = sideIndex;
        guard.ownedHere = ownedHere;
        guard.MoveHome();
    }

    private void MoveHome()
    {
        VersusMatch.Side side = match.GetSide(sideIndex);
        Transform package = dog.transform.parent;

        if (package != null && side.dogAnchor != null)
        {
            Transform bed = package.Find("BedTransform");
            Vector3 delta = side.dogAnchor.position - (bed != null ? bed.position : dog.transform.position);
            delta.y = 0f;
            package.position += delta;

            Vector3 dogPosition = side.dogAnchor.position + side.dogAnchor.forward * 1.2f;
            dogPosition.y = dog.transform.position.y;

            NavMeshAgent agent = dog.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(dogPosition);
            else dog.transform.position = dogPosition;
        }

        if (side.depotZone != null && PatrolBoundariesSetter != null)
            PatrolBoundariesSetter.Invoke(dog, new object[] { new Collider[] { side.depotZone } });
    }

    private void Update()
    {
        if (!ownedHere || busy || dog == null || !dog.enabled) return;
        if (match == null || match.Winner != VersusMatch.NoWinner) return;
        if (Time.time < nextScanAt) return;
        nextScanAt = Time.time + 0.2f;

        // A sleeping or eating dog finishes what it is doing.
        if (dog.currentDogState == DogStateMachine.DogState.Sleep || dog.currentDogState == DogStateMachine.DogState.Eat) return;

        float loyalty = dog.GetAttitudePercent();

        VersusBait bait = VersusBait.FindLooseNear(dog.transform.position, settings.baitRadius);
        if (bait != null)
        {
            float stall = Mathf.Lerp(settings.baitStallDisloyal, settings.baitStallLoyal, Mathf.Clamp01(loyalty / 100f));
            Begin(new VersusDogBaitState(dog, this, bait, stall));
            return;
        }

        if (Time.time < nextChargeAt || loyalty < settings.guardLoyalty) return;

        PlayerNetwork_MP thief = FindThief();
        if (thief != null) Begin(new VersusDogChargeState(dog, this, thief));
    }

    private PlayerNetwork_MP FindThief()
    {
        VersusMatch.Side side = match.GetSide(sideIndex);
        if (side.depotZone == null) return null;

        if (Time.time >= nextPlayerRefreshAt)
        {
            nextPlayerRefreshAt = Time.time + 1f;
            players = FindObjectsByType<PlayerNetwork_MP>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        Bounds zone = side.depotZone.bounds;
        PlayerNetwork_MP nearest = null;
        float nearestDistance = settings.guardRange;

        foreach (PlayerNetwork_MP player in players)
        {
            if (player == null || player.isDead || player.netId == side.playerNetId) continue;

            Vector3 position = player.transform.position;
            if (!zone.Contains(new Vector3(position.x, zone.center.y, position.z))) continue;
            if (IsDown(player)) continue;

            float distance = dog.movementHandler.CheckDistanceToTarget(player.transform);
            if (distance > nearestDistance) continue;

            nearestDistance = distance;
            nearest = player;
        }

        return nearest;
    }

    public static bool IsDown(PlayerNetwork_MP player)
    {
        return player.TryGetComponent(out PlayerStateMachine state) &&
               (state.currentPlayerState == PlayerStates.Stunned || state.currentPlayerState == PlayerStates.Dead);
    }

    private void Begin(State state)
    {
        busy = true;
        dog.SwitchState(state);
    }

    // The dog rammed the thief: the server stuns them like a car hit (which also knocks the item out of their hands).
    public void Ram(PlayerNetwork_MP thief)
    {
        nextChargeAt = Time.time + settings.chargeCooldown;
        dog.onBark?.Invoke();
        match.ReportDogHit(thief.netId);
    }

    // Hands the dog back to its normal routine. The versus states were entered behind the state machine's back,
    // so pick a state it does not think it is already in.
    public void Finish()
    {
        if (dog.currentDogState == DogStateMachine.DogState.Patrol) dog.ChangeToIdleState();
        else dog.ChangeToPatrolState();
    }

    public void OnStateExit()
    {
        busy = false;
        if (Time.time + 1f > nextChargeAt) nextChargeAt = Time.time + 1f;
    }
}

// Runs at the thief and rams them.
public class VersusDogChargeState : DogBaseState
{
    private readonly VersusDogGuard guard;
    private readonly PlayerNetwork_MP thief;
    private float timer;
    private float repathTimer;

    public VersusDogChargeState(DogStateMachine stateMachine, VersusDogGuard guard, PlayerNetwork_MP thief) : base(stateMachine)
    {
        this.guard = guard;
        this.thief = thief;
    }

    public override void Enter()
    {
        timer = 0f;
        stateMachine.onBark?.Invoke();
        Repath();
        stateMachine.animator.CrossFadeInFixedTime(DogChaseState.runHash, 0.1f);
    }

    public override void Tick(float deltaTime)
    {
        timer += deltaTime;
        repathTimer += deltaTime;

        if (thief == null || thief.isDead || VersusDogGuard.IsDown(thief) || timer > guard.Settings.chargeTimeout)
        {
            guard.Finish();
            return;
        }

        float distance = stateMachine.movementHandler.CheckDistanceToTarget(thief.transform);

        if (distance <= guard.Settings.hitDistance)
        {
            guard.Ram(thief);
            guard.Finish();
            return;
        }

        if (distance > guard.Settings.guardRange * 1.5f)
        {
            guard.Finish();
            return;
        }

        if (repathTimer >= 0.25f) Repath();
    }

    private void Repath()
    {
        repathTimer = 0f;
        stateMachine.movementHandler.SetTarget(thief.transform);
        stateMachine.movementHandler.SetBreakDistance(0.5f);
        stateMachine.movementHandler.SetRunning(true);
        stateMachine.movementHandler.MoveTowardsTarget();
    }

    public override void FixedTick(float fixedDeltaTime) { }

    public override void Exit()
    {
        guard.OnStateExit();
    }
}

// Runs to a bait and is busy with it for the stall time, then the bait is gone.
public class VersusDogBaitState : DogBaseState
{
    private const float ReachDistance = 1.2f;
    private const float GiveUpTime = 8f;

    private readonly VersusDogGuard guard;
    private readonly VersusBait bait;
    private readonly float stall;
    private float timer;
    private float repathTimer;
    private bool eating;

    public VersusDogBaitState(DogStateMachine stateMachine, VersusDogGuard guard, VersusBait bait, float stall) : base(stateMachine)
    {
        this.guard = guard;
        this.bait = bait;
        this.stall = stall;
    }

    public override void Enter()
    {
        timer = 0f;
        eating = false;
        bait.Claimed = true;
        Repath();
        stateMachine.animator.CrossFadeInFixedTime(DogChaseState.runHash, 0.1f);
    }

    public override void Tick(float deltaTime)
    {
        // Someone picked the bait back up.
        if (bait == null || !bait.IsLoose)
        {
            guard.Finish();
            return;
        }

        timer += deltaTime;

        if (eating)
        {
            if (timer < stall) return;

            bait.Consume();
            guard.Finish();
            return;
        }

        if (stateMachine.movementHandler.CheckDistanceToTarget(bait.transform) <= ReachDistance)
        {
            eating = true;
            timer = 0f;
            stateMachine.movementHandler.StopAllMovement();
            stateMachine.animator.CrossFadeInFixedTime(DogEatState.eatHash, 0.1f);
            return;
        }

        if (timer > GiveUpTime)
        {
            guard.Finish();
            return;
        }

        repathTimer += deltaTime;
        if (repathTimer >= 0.5f) Repath();
    }

    private void Repath()
    {
        repathTimer = 0f;
        stateMachine.movementHandler.SetTarget(bait.transform);
        stateMachine.movementHandler.SetBreakDistance(0.6f);
        stateMachine.movementHandler.SetRunning(true);
        stateMachine.movementHandler.MoveTowardsTarget();
    }

    public override void FixedTick(float fixedDeltaTime) { }

    public override void Exit()
    {
        if (bait != null) bait.Claimed = false;
        guard.OnStateExit();
    }
}
