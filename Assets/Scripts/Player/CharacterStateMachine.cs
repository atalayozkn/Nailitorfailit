public class CharacterStateMachine
{
    public ICharacterState CurrentState { get; private set; }

    public void ChangeState(ICharacterState newState)
    {
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Tick()
    {
        CurrentState?.Tick();
    }
}