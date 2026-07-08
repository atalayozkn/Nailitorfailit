using UnityEngine;

public class PlayerFaintedState_SP : PlayerBaseState_SP
{
    public static readonly int faintedHash = Animator.StringToHash("faint");
    public static readonly int standHash = Animator.StringToHash("stand");

    private float counterTime;
    private float faintedDuration;
    private float standDuration;

    public PlayerFaintedState_SP(PlayerStateMachine_SP stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.animator.CrossFadeInFixedTime(faintedHash, 0.1f);
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
            stateMachine.ChangeToIdleState();
            counterTime = 0;
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
    }
}
