using UnityEngine;

public class PlayerFaintState : PlayerBaseState
{
    public static readonly int faintedHash = Animator.StringToHash("Faint");
    public static readonly int standHash = Animator.StringToHash("Stand");

    private float counterTime;
    private float faintedDuration = 2.4f;
    private float standDuration = 3.1f;

    bool isFaintComplete = false;

    public PlayerFaintState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.animator.CrossFadeInFixedTime(faintedHash, 0.1f);
        stateMachine.rb.linearVelocity = Vector3.zero;
        counterTime = 0;
    }
    public override void Tick(float deltaTime)
    {
        counterTime += deltaTime;

        if (counterTime >= faintedDuration && !isFaintComplete)
        {
            stateMachine.animator.CrossFadeInFixedTime(standHash, 0f);
            counterTime = 0;
            isFaintComplete = true;
        }

        if(counterTime >= standDuration)
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
    }
}
