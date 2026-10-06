using UnityEngine;

public class DogEatState : DogBaseState
{
    public static readonly int runHash = Animator.StringToHash("Run");
    public static readonly int eatHash = Animator.StringToHash("Eat");
    public static readonly int unHappyHash = Animator.StringToHash("UnHappy");

    private bool hasEaten;
    private float counter;
    private float currentAnimDuration = 3.0f;
    public DogEatState(DogStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        hasEaten = false;
        counter = 0;

        stateMachine.movementHandler.SetRunning(true);
        stateMachine.movementHandler.SetBreakDistance(0.5f);
        stateMachine.movementHandler.SetTargetAsBowl();
        stateMachine.movementHandler.MoveTowardsTarget();
        stateMachine.animator.CrossFadeInFixedTime(runHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        if (!hasEaten) return;

        counter += deltaTime;

        if (counter >= currentAnimDuration)
        {
            stateMachine.ChangeToPatrolState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {
        if (stateMachine.movementHandler.IsMoving()) return;
        if (!hasEaten) Eat();
    }
    public override void Exit()
    {

    }
    private void Eat()
    {
        hasEaten = true;

        if (stateMachine.foodBowl.HasEnoughFood(stateMachine.perEatConsumption))
        {
            stateMachine.foodBowl.ConsumeFood(stateMachine.perEatConsumption);
            stateMachine.satietyController.GainSatiety(stateMachine.perEatGain);
            stateMachine.animator.CrossFadeInFixedTime(eatHash, 0.1f);
        }
        else
        {
            stateMachine.animator.CrossFadeInFixedTime(unHappyHash, 0.1f);
        }
    }
}
