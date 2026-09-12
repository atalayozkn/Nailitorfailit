using Interactions;
using UnityEngine;
public class PlayerUseState : PlayerBaseState
{
    private static readonly int useFEHash = Animator.StringToHash("UseFE");
    private static readonly int useEDHash = Animator.StringToHash("UseED");
    private float counter;
    private float fullDuration;
    private bool isTransitionComplete;
    private UseType currentUseType;
    public PlayerUseState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        counter = 0;
        isTransitionComplete = false;
        stateMachine.movementHandler.SetActivity(false);
        stateMachine.interactionHandler.SetActivity(false);
        stateMachine.useHandler.SetActivity(false);

        currentUseType = stateMachine.useHandler.GetCurrentUseType();

        switch (currentUseType) //Switch Animation based on this
        {
            case UseType.EnergyDrink:
                fullDuration = 3.0f;
                stateMachine.animator.CrossFadeInFixedTime(useEDHash,0f);
                break;
            case UseType.FireExtinguisher:
                fullDuration = 1.0f;
                stateMachine.animator.CrossFadeInFixedTime(useFEHash, 0f);
                break;
            default:
                stateMachine.ChangeToIdleState();
                break;
        }
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter >= fullDuration && !isTransitionComplete)
        {
            isTransitionComplete = true;
            IUsable usable = stateMachine.useHandler.GetCurrentUsable();
            if (usable != null) usable.OnUse();
            stateMachine.ForceUpdateIdle();
            return;
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
        stateMachine.movementHandler.SetActivity(true);
        stateMachine.interactionHandler.SetActivity(true);
        stateMachine.useHandler.SetActivity(true);
    }
}