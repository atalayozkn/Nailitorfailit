using UnityEngine;
public class DogAidState : DogBaseState
{
    public DogAidState(DogStateMachine stateMachine) : base(stateMachine) { }
    public static readonly int groundSearchHash = Animator.StringToHash("SearchGround");
    public static readonly int runHash = Animator.StringToHash("Run");

    private float searchDuration = 3.0f;
    private float counter = 0f;
    private bool hasSpawned = false;
    private bool hasTargetSet = false;
    public override void Enter()
    {
        counter = 0f;
        stateMachine.animator.CrossFadeInFixedTime(groundSearchHash, 0f);
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter < searchDuration) return; 

        if (!hasSpawned)
        {
            //Spawn Object
            hasSpawned = true;
            stateMachine.SpawnObject();
        }

        if (!hasTargetSet)
        {
            hasTargetSet = true;
            stateMachine.movementHandler.SetRunning(true);
            stateMachine.movementHandler.SetBreakDistance(0.1f);
            stateMachine.movementHandler.SetTarget(stateMachine.itemDropTransfom);
            stateMachine.movementHandler.MoveTowardsTarget();
        }

        if (stateMachine.movementHandler.IsMoving()) return;

        stateMachine.currentCarriable.DropByDog(stateMachine.itemDropTransfom);
        stateMachine.ChangeToIdleState();
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {

    }
}
