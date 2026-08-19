using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public static readonly int idleHash = Animator.StringToHash("idle");
    public static readonly int carryIdleHash = Animator.StringToHash("carryIdle");

    public override void Enter()
    {
        int targetHash = stateMachine.interactionHandler.IsCarrying() ? carryIdleHash : idleHash;
        stateMachine.animator.CrossFadeInFixedTime(targetHash, 0.1f);
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