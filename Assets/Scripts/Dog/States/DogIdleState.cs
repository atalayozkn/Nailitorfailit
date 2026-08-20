using UnityEngine;

public class DogIdleState : DogBaseState
{
    public static readonly int sitHash = Animator.StringToHash("Sit");
    public static readonly int idleSitHash = Animator.StringToHash("IdleSit");

    private float counter;
    private float sitDuration = 1.0f;
    private float idleTime = 3.0f;
    private bool sitComplete;
    public DogIdleState(DogStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        counter = 0f;
        sitComplete = false;
        stateMachine.animator.CrossFadeInFixedTime(sitHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter > sitDuration && !sitComplete)
        {
            sitComplete = true;
            stateMachine.animator.CrossFadeInFixedTime(idleSitHash, 0.1f);
        }

        if (counter >= (sitDuration + idleTime))
        {
            stateMachine.ChangeToPatrolState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {
       
    }
    public override void Exit()
    {
        
    }
}
