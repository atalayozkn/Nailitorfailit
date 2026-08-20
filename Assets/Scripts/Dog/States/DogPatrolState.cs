using UnityEngine;

public class DogPatrolState : DogBaseState
{
    public static readonly int walkHash = Animator.StringToHash("Walk");
    public DogPatrolState(DogStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.RandomizePatrolTarget();
        stateMachine.movementHandler.SetTarget(stateMachine.patrolTarget);
        stateMachine.movementHandler.SetRunning(false);
        stateMachine.movementHandler.SetBreakDistance(stateMachine.patrolBreakDistance);
        stateMachine.movementHandler.MoveTowardsTarget();
        stateMachine.animator.CrossFadeInFixedTime(walkHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {

    }
    public override void FixedTick(float fixedDeltaTime)
    {
        if (stateMachine.movementHandler.IsMoving()) return;
        stateMachine.ChangeToInspectState();
    }
    public override void Exit()
    {

    }
}
