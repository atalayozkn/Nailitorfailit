using UnityEngine;

public class PlayerFaintState : PlayerBaseState
{
    public static readonly int faintedHash = Animator.StringToHash("Faint");
    public static readonly int standHash = Animator.StringToHash("Stand");

    private float counterTime;
    private float faintedDuration = 2.4f;
    private float standDuration = 3.1f;

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
        if (counterTime > faintedDuration)
        {
            stateMachine.animator.CrossFadeInFixedTime(standHash, 0.1f);
        }
        if(counterTime > standDuration + faintedDuration)
        {
            stateMachine.ForceSwitchToIdleState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
    }
}
