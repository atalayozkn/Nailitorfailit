using UnityEngine;

public class PlayerStandState : PlayerBaseState
{
    public static readonly int standHash = Animator.StringToHash("Stand");
    private float standDuration = 2.0f;
    private float counter;
    public PlayerStandState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        counter = 0;
        stateMachine.movementHandler.SetActivity(false);
        stateMachine.interactionHandler.SetActivity(false);
        stateMachine.animator.CrossFadeInFixedTime(standHash, 0f);
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter >= standDuration)
        {
            if (stateMachine.ShouldRecoverFromSlip()) stateMachine.ForceSwitchToIdleState();
            else stateMachine.ChangeToSlippingState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
        stateMachine.movementHandler.SetActivity(true);
        stateMachine.interactionHandler.SetActivity(true);
    }
}
