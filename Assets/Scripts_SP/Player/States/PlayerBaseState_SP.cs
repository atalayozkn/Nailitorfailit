public abstract class PlayerBaseState_SP : State
{
    protected PlayerStateMachine_SP stateMachine;

    public PlayerBaseState_SP(PlayerStateMachine_SP stateMachine)
    {
        this.stateMachine = stateMachine;
    }
}