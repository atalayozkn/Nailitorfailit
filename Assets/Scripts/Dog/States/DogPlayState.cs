using ItemScript;
using UnityEngine;

public class DogPlayState : DogBaseState
{
    public DogPlayState(DogStateMachine stateMachine) : base(stateMachine) { }
    public static readonly int runHash = Animator.StringToHash("Run");

    private CarriableObject_SP currentCarriable;
    private float counter;
    private float playTime = 10f;
    private bool hasPickedUp;

    public override void Enter()
    {
        currentCarriable = stateMachine.playTarget.GetComponent<CarriableObject_SP>();

        if (currentCarriable == null)
        {
            stateMachine.ChangeToIdleState();
            return;
        }

        stateMachine.movementHandler.SetRunning(true);
        stateMachine.movementHandler.SetBreakDistance(0.1f);
        stateMachine.movementHandler.SetTarget(currentCarriable.transform);
        stateMachine.movementHandler.MoveTowardsTarget();
        stateMachine.animator.CrossFadeInFixedTime(runHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (stateMachine.movementHandler.IsMoving()) return;

        if (currentCarriable != null && !hasPickedUp && !currentCarriable.IsOccupied())
        {
            hasPickedUp = true;
            currentCarriable.PickUpByDog(stateMachine.carryTransform);
            stateMachine.RandomizePatrolTarget();
            stateMachine.movementHandler.SetRunning(true);
            stateMachine.movementHandler.SetBreakDistance(0.5f);
            stateMachine.movementHandler.SetTarget(stateMachine.patrolTarget);
            stateMachine.movementHandler.MoveTowardsTarget();
            stateMachine.animator.CrossFadeInFixedTime(runHash, 0.1f);
            return;
        }

        if (counter > playTime)
        {
            //Only get aggressive about an item the dog actually played with.
            if (hasPickedUp && stateMachine.favorController.GetPercentFavor() <= 50)
            {
                stateMachine.SetCurrentCarriable(currentCarriable);
                stateMachine.ChangeToAggresiveState();
            }
            else
            {
                if (hasPickedUp) currentCarriable.DropByDog(stateMachine.dropTransform);
                currentCarriable = null;
                stateMachine.ChangeToIdleState();
            }
        }
        else
        {
            stateMachine.RandomizePatrolTarget();
            stateMachine.movementHandler.SetRunning(true);
            stateMachine.movementHandler.SetBreakDistance(0.5f);
            stateMachine.movementHandler.SetTarget(stateMachine.patrolTarget);
            stateMachine.movementHandler.MoveTowardsTarget();
            stateMachine.animator.CrossFadeInFixedTime(runHash, 0.1f);
        }

    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
        //Only drop if the dog actually picked it up, otherwise we'd yank it from the player/station.
        if (currentCarriable != null && hasPickedUp)
        {
            currentCarriable.DropByDog(stateMachine.dropTransform);
        }
        currentCarriable = null;
        hasPickedUp = false;
    }
}
