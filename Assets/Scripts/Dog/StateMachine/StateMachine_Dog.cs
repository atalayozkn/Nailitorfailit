using UnityEngine;

public abstract class StateMachine_Dog : MonoBehaviour
{
    protected State currentState;
    public bool hasStateDebug;
    public void SwitchState(State state)
    {
        currentState?.Exit();
        currentState = state;
        currentState?.Enter();
        if (hasStateDebug) Debug.Log($"Current Player State: {currentState}");
    }
    public void FixedUpdate()
    {
        currentState?.FixedTick(Time.fixedDeltaTime);
    }
    public void Update()
    {
        currentState?.Tick(Time.deltaTime);
    }
}
