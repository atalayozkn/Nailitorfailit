using UnityEngine;

public class PlayerNavigationState : PlayerBaseState
{
    public PlayerNavigationState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public static readonly int walkHash = Animator.StringToHash("walk");
    public static readonly int runHash = Animator.StringToHash("run");
    public static readonly int carryWalkHash = Animator.StringToHash("carryWalk");
    public static readonly int carryRunHash = Animator.StringToHash("carryRun");

    private int currentAnimationHash;

    public override void Enter()
    {
        currentAnimationHash = 0;
        UpdateNavigationAnimation();
    }

    public override void Tick(float deltaTime)
    {
        UpdateNavigationAnimation();
    }

    public override void FixedTick(float fixedDeltaTime)
    {

    }

    public override void Exit()
    {

    }
    private void UpdateNavigationAnimation()
    {
        int targetHash;
        bool isCarrying = stateMachine.interactionHandler.IsCarrying();
        bool isRunning = stateMachine.movementHandler.IsRunning();

        if (isCarrying)
        {
            if (isRunning)
            {
                targetHash = carryRunHash;
            }
            else
            {
                targetHash = carryWalkHash;
            }
        }
        else
        {
            if (isRunning)
            {
                targetHash = runHash;
            }
            else
            {
                targetHash = walkHash;
            }
        }

        if (targetHash != currentAnimationHash)
        {
            currentAnimationHash = targetHash;
            stateMachine.animator.CrossFadeInFixedTime(targetHash, 0.1f);
        }
    }
}