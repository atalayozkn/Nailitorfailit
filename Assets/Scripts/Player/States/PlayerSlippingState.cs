using UnityEngine;

public class PlayerSlippingState : PlayerBaseState
{
    public static readonly int slipHash = Animator.StringToHash("Slip");

    // The slip used to end on the first frame the slippery surface was no longer under the player: the stumble was gone
    // before it was drawn, which read as "he stands up instantly" (worst while carrying something, the drop pushed him
    // off the puddle straight away). A short grace period keeps the animation visible; the puddle still decides after it.
    private const float MinimumSlipTime = 0.45f;
    private float enteredAt;

    public PlayerSlippingState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        enteredAt = Time.time;
        stateMachine.animator.CrossFadeInFixedTime(slipHash, 0f);
        stateMachine.movementHandler.SetSlipping(true);
        stateMachine.startSlipEvent?.Invoke();
        VfxManager.Play(VfxId.PlayerSlip, stateMachine.transform.position);

        var carriable = stateMachine.interactionHandler.GetCurrentCarriable();
        if (carriable != null)
        {
            // Multiplayer: the drop goes through the server so every machine sees it (a local drop left the item in the
            // hand of the other copies).
            if (NetworkModeHelper.IsNetworked && carriable.TryGetComponent(out CarriableObject_MP networked))
                networked.NotifyDropped(stateMachine.interactionHandler);
            else
                carriable.OnDrop(true);
        }
    }
    public override void Tick(float deltaTime)
    {
       
    }
    public override void FixedTick(float fixedDeltaTime)
    {
        if (Time.time - enteredAt < MinimumSlipTime) return;
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
