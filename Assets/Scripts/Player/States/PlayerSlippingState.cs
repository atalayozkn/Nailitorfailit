using UnityEngine;

public class PlayerSlippingState : PlayerBaseState
{
    public static readonly int slipHash = Animator.StringToHash("Slip");
    public PlayerSlippingState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.animator.CrossFadeInFixedTime(slipHash, 0f);
        stateMachine.movementHandler.SetSlipping(true);
        stateMachine.startSlipEvent?.Invoke();

        var carriable = stateMachine.interactionHandler.GetCurrentCarriable();
        if (carriable != null) carriable.OnDrop(true);
    }
    public override void Tick(float deltaTime)
    {
       
    }
    public override void FixedTick(float fixedDeltaTime)
    {
        if (!stateMachine.ShouldRecoverFromSlip()) return;

        if (!stateMachine.movementHandler.IsRunning())
        {
            stateMachine.ChangeToIdleState();
            return;
        }

        if (stateMachine.movementHandler.IsRunning())
        {
            stateMachine.ChangeToStunnedState();
        }
    }
    public override void Exit()
    {
        stateMachine.movementHandler.SetSlipping(false);
        stateMachine.stopSlipEvent?.Invoke();
    }
}
