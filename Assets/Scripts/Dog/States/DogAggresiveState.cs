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
            //Only destroy the item the dog was playing with, not whatever the presence checker last saw.
            var carriable = stateMachine.currentCarriable;
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
        //Don't let the played item linger as a target for later states.
        stateMachine.SetCurrentCarriable(null);
    }
}
