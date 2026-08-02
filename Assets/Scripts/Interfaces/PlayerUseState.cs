using UnityEngine;

public class PlayerUseState : PlayerBaseState
{
    private static readonly int useAnimationHash =
        Animator.StringToHash("Consume");

    private float counter;

    public PlayerUseState(PlayerStateMachine stateMachine)
        : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.movementHandler.SetActivity(false);
        stateMachine.interactionHandler.SetActivity(false);

        counter = stateMachine.useDuration;

        stateMachine.animator.CrossFadeInFixedTime(
            useAnimationHash,
            0.1f
        );
    }

    public override void Tick(float deltaTime)
    {
        counter -= deltaTime;

        if (counter <= 0f)
        {
            stateMachine.ChangeToIdleState();
        }
    }

    public override void FixedTick(float fixedDeltaTime)
    {
    }

    public override void Exit()
    {
        if (stateMachine.currentPlayerState == PlayerStates.Dead)
        {
            return;
        }

        stateMachine.movementHandler.SetActivity(true);
        stateMachine.interactionHandler.SetActivity(true);
    }
}