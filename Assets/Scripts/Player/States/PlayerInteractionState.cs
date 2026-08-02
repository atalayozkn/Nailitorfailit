using UnityEngine;
public class PlayerInteractionState : PlayerBaseState
{
    public PlayerInteractionState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    private static readonly int interactionAnimHash = Animator.StringToHash("Interact");
    public override void Enter()
    {
        stateMachine.animator.CrossFadeInFixedTime(interactionAnimHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        if (!stateMachine.interactionHandler.IsInteracting())
        {
            if (stateMachine.movementHandler.IsMoving())
            {
                stateMachine.ChangeToNavigationState();
            }
            else
            {
                stateMachine.ChangeToIdleState();
            }
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
    }
}
