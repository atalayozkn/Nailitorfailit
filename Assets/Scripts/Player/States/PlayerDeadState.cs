using UnityEngine;

public class PlayerDeadState : PlayerBaseState
{
    public static readonly int FireDeathHash = Animator.StringToHash("Death_Fire");
    public static readonly int ElectricityDeathHash = Animator.StringToHash("Death_Electricty");
    public PlayerDeadState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.playerRenderer.enabled = false;
        stateMachine.movementHandler.enabled = false;
        stateMachine.interactionHandler.enabled = false;

        stateMachine.SetDead(true);

        switch (stateMachine.currentReason)
        {
            case DeathReason.Car:
                stateMachine.carDeathEvent?.Invoke();
                stateMachine.respawnManager.RespawnPlayer(stateMachine);
                break;
            case DeathReason.Fire:
                stateMachine.fireDeathEvent?.Invoke();
                stateMachine.animator.CrossFadeInFixedTime(FireDeathHash, 0f);
                stateMachine.respawnManager.RespawnPlayer(stateMachine);
                break;
            case DeathReason.Electricty:
                stateMachine.electricityDeathEvent?.Invoke();
                stateMachine.animator.CrossFadeInFixedTime(ElectricityDeathHash, 0f);
                stateMachine.respawnManager.RespawnPlayer(stateMachine);
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
        stateMachine.playerRenderer.enabled = true;
        stateMachine.movementHandler.enabled = true;
        stateMachine.interactionHandler.enabled = true;
        stateMachine.SetDead(false);
    }
}
