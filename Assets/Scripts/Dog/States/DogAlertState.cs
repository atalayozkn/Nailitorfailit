using UnityEngine;
using UnityEngine.AI;

// A loyal dog's hazard alert (singleplayer): runs to the player, barks facing the hazard, then leads the player there
// and keeps barking beside it until it is fixed or over. Any phase ends early once the hazard is no longer fixable.
public class DogAlertState : DogBaseState
{
    private static readonly int runHash = Animator.StringToHash("Run");
    private static readonly int barkHash = Animator.StringToHash("Bark");

    private enum Phase { ToPlayer, BarkAtPlayer, ToHazard, BarkAtHazard }

    private const float PlayerBreakDistance = 1.8f;
    private const float HazardBreakDistance = 1.0f;
    private const float BarkAtPlayerDuration = 2f;
    private const float RebarkInterval = 3f;
    private const float RunTimeout = 12f;
    private const float TurnSpeed = 8f;

    private static Transform hazardPoint; // shared scratch target, the dog movement handler follows a Transform

    private readonly TelegraphedHazard hazard;
    private Transform player;
    private Phase phase;
    private float timer;

    public DogAlertState(DogStateMachine stateMachine, TelegraphedHazard hazard) : base(stateMachine)
    {
        this.hazard = hazard;
    }

    public override void Enter()
    {
        var handler = Object.FindFirstObjectByType<PlayerInteractionHandler>();
        player = handler != null ? handler.transform : null;
        if (player == null) { stateMachine.EndAlert(); return; }

        // Inspect can be carrying a gift and does not drop it on exit; let it go before running off.
        if (stateMachine.currentCarriable != null)
        {
            stateMachine.currentCarriable.DropByDog(stateMachine.dropTransform);
            stateMachine.SetCurrentCarriable(null);
        }

        if (hazardPoint == null) hazardPoint = new GameObject("DogAlertTarget").transform;
        hazardPoint.position = StandPoint();

        RunTo(player, PlayerBreakDistance, Phase.ToPlayer);
    }
    public override void Tick(float deltaTime)
    {
        if (hazard == null || !hazard.IsRepairable) { stateMachine.EndAlert(); return; }
        timer += deltaTime;

        switch (phase)
        {
            case Phase.ToPlayer:
                if (stateMachine.movementHandler.IsMoving() && timer < RunTimeout) return;
                StartBark(Phase.BarkAtPlayer);
                break;
            case Phase.BarkAtPlayer:
                Face(hazard.transform.position, deltaTime);
                if (timer < BarkAtPlayerDuration) return;
                RunTo(hazardPoint, HazardBreakDistance, Phase.ToHazard);
                break;
            case Phase.ToHazard:
                if (stateMachine.movementHandler.IsMoving() && timer < RunTimeout) return;
                StartBark(Phase.BarkAtHazard);
                break;
            case Phase.BarkAtHazard:
                Face(player.position, deltaTime); // looks back at the player while barking at the spot
                if (timer < RebarkInterval) return;
                StartBark(Phase.BarkAtHazard);
                break;
        }
    }
    public override void FixedTick(float fixedDeltaTime) { }
    public override void Exit()
    {
        stateMachine.movementHandler.StopAllMovement();
    }

    private void RunTo(Transform target, float breakDistance, Phase next)
    {
        phase = next;
        timer = 0f;
        stateMachine.movementHandler.SetTarget(target);
        stateMachine.movementHandler.SetBreakDistance(breakDistance);
        stateMachine.movementHandler.SetRunning(true);
        stateMachine.movementHandler.MoveTowardsTarget();
        stateMachine.animator.CrossFadeInFixedTime(runHash, 0.1f);
    }
    private void StartBark(Phase next)
    {
        phase = next;
        timer = 0f;
        stateMachine.movementHandler.StopAllMovement();
        stateMachine.animator.CrossFadeInFixedTime(barkHash, 0.1f);
        stateMachine.onBark?.Invoke();
    }
    private void Face(Vector3 point, float deltaTime)
    {
        Vector3 dir = point - stateMachine.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, Quaternion.LookRotation(dir), TurnSpeed * deltaTime);
    }
    // A walkable spot by the hazard: sockets sit on a wall, so step out along their facing first.
    private Vector3 StandPoint()
    {
        Vector3 p = hazard.transform.position + hazard.transform.forward * 0.6f;
        p.y = 0f;
        return NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : hazard.transform.position;
    }
}
