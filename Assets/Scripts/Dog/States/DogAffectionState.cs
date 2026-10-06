using UnityEngine;

public class DogAffectionState : DogBaseState
{
    public static readonly int affectionHash = Animator.StringToHash("Affection");
    public DogAffectionState(DogStateMachine stateMachine) : base(stateMachine) { }
    private float counter;
    private float animDuration = 3.0f;
    public override void Enter()
    {
        counter = 0;
        stateMachine.animator.CrossFadeInFixedTime(affectionHash,0f);
        stateMachine.favorController.GainFavor(stateMachine.perPetFavorGain);
        stateMachine.movementHandler.StopAllMovement();
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;
        if (counter >= animDuration)
        {
            stateMachine.ChangeToPatrolState();
            return;
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {

    }
}
