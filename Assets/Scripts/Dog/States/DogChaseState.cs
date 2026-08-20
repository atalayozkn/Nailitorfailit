using UnityEngine;

public class DogChaseState : DogBaseState
{
    public static readonly int runHash = Animator.StringToHash("Run");
    public DogChaseState(DogStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.movementHandler.SetTarget(stateMachine.moveTarget);
        stateMachine.movementHandler.SetBreakDistance(stateMachine.chaseBreakDistance);
        stateMachine.movementHandler.SetRunning(true);
        stateMachine.movementHandler.MoveTowardsTarget();
        stateMachine.animator.CrossFadeInFixedTime(runHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {

    }
    public override void FixedTick(float fixedDeltaTime)
    {
        if (stateMachine.movementHandler.IsMoving()) return;
        stateMachine.ChangeToBarkState();
    }
    public override void Exit()
    {
        stateMachine.movementHandler.SetRunning(false);
    }
}
