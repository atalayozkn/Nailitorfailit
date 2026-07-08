using UnityEngine;

public class PlayerIdleState_SP : PlayerBaseState_SP
{
    public PlayerIdleState_SP(PlayerStateMachine_SP stateMachine) : base(stateMachine) { }

    public static readonly int idleHash = Animator.StringToHash("idle");
    public static readonly int carryIdleHash = Animator.StringToHash("carryIdle");

    private int currentAnimationHash;

    public override void Enter()
    {
        currentAnimationHash = 0;
        UpdateIdleAnimation();
    }

    public override void Tick(float deltaTime)
    {
        UpdateIdleAnimation();
    }

    public override void FixedTick(float fixedDeltaTime)
    {
    }

    public override void Exit()
    {
    }

    private void UpdateIdleAnimation()
    {
        int targetHash = stateMachine.IsCarrying() ? carryIdleHash : idleHash;

        if (currentAnimationHash == targetHash)
            return;

        currentAnimationHash = targetHash;

        stateMachine.animator.CrossFadeInFixedTime(targetHash, 0.1f);
    }
}