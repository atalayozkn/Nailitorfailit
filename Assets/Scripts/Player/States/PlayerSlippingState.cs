using UnityEngine;

public class PlayerSlippingState : PlayerBaseState
{
    public static readonly int slipHash = Animator.StringToHash("Slip");
    public PlayerSlippingState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.animator.CrossFadeInFixedTime(slipHash, 0.1f);
        stateMachine.movementHandler.SetSlipping(true);
        stateMachine.crashHelper.SetActivity(true);

        var carriable = stateMachine.interactionHandler.GetCurrentCarriable();
        if (carriable != null) carriable.OnDrop(true);
    }
    public override void Tick(float deltaTime)
    {
       
    }
    public override void FixedTick(float fixedDeltaTime)
    {
        stateMachine.SetCrashActivity();

        if (stateMachine.ShouldRecoverFromSlip())
        {
            stateMachine.ForceSwitchToIdleState();
        }
    }
    public override void Exit()
    {
        stateMachine.crashHelper.SetActivity(false);
        stateMachine.movementHandler.SetSlipping(false);
    }
}
