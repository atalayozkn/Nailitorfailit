using UnityEngine;

public class PlayerStunnedState : PlayerBaseState
{
    private float counterTime;
    private float stunDuration = 3f;
    public PlayerStunnedState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.UpdateMoveDirection();
        stateMachine.SetRagdoll(true);
        counterTime = 0;
    }
    public override void Tick(float deltaTime)
    {
        counterTime += deltaTime;

        if (counterTime >= stunDuration)
        {
            stateMachine.ChangeToStandState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
        stateMachine.SetRagdoll(false);
    }
}
