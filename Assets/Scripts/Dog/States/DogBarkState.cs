using UnityEngine;

public class DogBarkState : DogBaseState
{
    public static readonly int barkHash = Animator.StringToHash("Bark");
    public DogBarkState(DogStateMachine stateMachine) : base(stateMachine) { }
    private float counter;
    private float animDuration = 3.0f;
    public override void Enter()
    {
        counter = 0f;
        stateMachine.animator.CrossFadeInFixedTime(barkHash, 0.1f);
        stateMachine.mailmanController.Scare();
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;
        if (counter >= animDuration && stateMachine.ShouldContinueChase())
        {
            stateMachine.ChangeToChaseState();
        }


    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {

    }
}
