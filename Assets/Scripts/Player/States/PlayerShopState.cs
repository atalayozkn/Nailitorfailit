using UnityEngine;

public class PlayerShopState : PlayerBaseState
{
    public PlayerShopState(PlayerStateMachine stateMachine) : base(stateMachine)  { }
    public override void Enter()
    {
        stateMachine.movementHandler.enabled = false;
        stateMachine.interactionHandler.enabled = false;
        Debug.Log("Entering ShopState");
    }
    public override void Tick(float deltaTime)
    {

    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
        stateMachine.movementHandler.enabled = true;
        stateMachine.interactionHandler.enabled= true;
        Debug.Log("Exiting ShopState");
    }
}