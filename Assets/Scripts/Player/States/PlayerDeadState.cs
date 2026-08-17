using UnityEngine;

public class PlayerDeadState : PlayerBaseState
{
    public static readonly int FireDeathHash = Animator.StringToHash("Death_Fire");
    public static readonly int ElectricityDeathHash = Animator.StringToHash("Death_Electricty");
    public PlayerDeadState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.SetDead(true);

        switch (stateMachine.currentReason)
        {
            case DeathReason.Car:
                stateMachine.respawnManager.RespawnPlayer(stateMachine.gameObject);
                break;
            case DeathReason.Fire:
                stateMachine.animator.CrossFadeInFixedTime(FireDeathHash, 0f);
                stateMachine.respawnManager.RespawnPlayer(stateMachine.gameObject);
                break;
            case DeathReason.Electricty:
                stateMachine.animator.CrossFadeInFixedTime(ElectricityDeathHash, 0f);
                stateMachine.respawnManager.RespawnPlayer(stateMachine.gameObject);
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
        stateMachine.SetDead(false);
    }
}
