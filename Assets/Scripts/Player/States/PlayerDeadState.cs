using UnityEngine;

public class PlayerDeadState : PlayerBaseState
{
    public static readonly int CarDeathHash = Animator.StringToHash("Death_Car");
    public static readonly int FireDeathHash = Animator.StringToHash("Death_Fire");
    public static readonly int ElectricityDeathHash = Animator.StringToHash("Death_Electricty");
    public PlayerDeadState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        switch (stateMachine.currentReason)
        {
            case DeathReason.Car:
                stateMachine.animator.CrossFadeInFixedTime(CarDeathHash, 0f);
                break;
            case DeathReason.Fire:
                stateMachine.animator.CrossFadeInFixedTime(FireDeathHash, 0f);
                break;
            case DeathReason.Electricty:
                stateMachine.animator.CrossFadeInFixedTime(ElectricityDeathHash, 0f);
                break;
        }
    }
    public override void Tick(float deltaTime)
    {

    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {

    }
}
