using UnityEngine;

public class PlayerDeadState : PlayerBaseState
{
    public static readonly int deadHash = Animator.StringToHash("dead");

    public PlayerDeadState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.animator.CrossFadeInFixedTime(deadHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {

    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {

    }
}
