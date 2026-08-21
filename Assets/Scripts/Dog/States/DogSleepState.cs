using Unity.Multiplayer.Tools.NetStatsMonitor;
using UnityEngine;

public class DogSleepState : DogBaseState
{
    public static readonly int walkHash = Animator.StringToHash("Walk");
    public static readonly int sleepHash = Animator.StringToHash("Sleep");

    private float counter;
    private float sleepDuration = 10f;
    public DogSleepState(DogStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        counter = 0f;
        stateMachine.movementHandler.SetTargetAsBed();
        stateMachine.movementHandler.SetRunning(false);
        stateMachine.movementHandler.SetBreakDistance(stateMachine.patrolBreakDistance);
        stateMachine.movementHandler.MoveTowardsTarget();
        stateMachine.animator.CrossFadeInFixedTime(walkHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;
        if (stateMachine.movementHandler.IsMoving()) return;
        stateMachine.animator.CrossFadeInFixedTime(sleepHash,0.1f);
        if (counter > sleepDuration)
        {
            stateMachine.energyController.GainEnergy(stateMachine.perSleepGain);
        }

    }
    public override void FixedTick(float fixedDeltaTime)
    {
    }
    public override void Exit()
    {

    }
}
