using ItemScript;
using UnityEngine;

public class DogAggresiveState : DogBaseState
{
    public static readonly int aggresiveHash = Animator.StringToHash("Aggresive");
    public DogAggresiveState(DogStateMachine stateMachine) : base(stateMachine) { }

    private float counter;
    private float animDuration;
    public override void Enter()
    {
        counter = 0f;
        stateMachine.animator.CrossFadeInFixedTime(aggresiveHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter > animDuration)
        {
            var carriable = stateMachine.presenceChecker.GetCurrentCarriable();
            if (carriable != null)
            {
                carriable.DestroyedByDog();
            }

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
