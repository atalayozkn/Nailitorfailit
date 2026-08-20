public abstract class DogBaseState : State
{
    protected DogStateMachine stateMachine;

    public DogBaseState(DogStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }
}