public abstract class PlayerBaseState_SP : State
{
    protected PlayerStateMachine stateMachine;

    public PlayerBaseState_SP(PlayerStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }
}