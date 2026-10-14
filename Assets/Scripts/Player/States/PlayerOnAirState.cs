using UnityEngine;

public class PlayerOnAirState : PlayerBaseState
{
    public static readonly int jumpHash = Animator.StringToHash("Jump");
    public PlayerOnAirState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    private float animDuration = 1.5f;
    private float counter = 0f;
    public override void Enter()
    {
        stateMachine.animator.CrossFadeInFixedTime(jumpHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;
        if (counter >= animDuration)
        {
            stateMachine.ChangeToNavigationState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {
       
    }
    public override void Exit()
    {
        stateMachine.movementHandler.SetJumping(false);
    }
}