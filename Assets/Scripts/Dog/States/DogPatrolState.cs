using UnityEngine;

public class DogPatrolState : DogBaseState
{
    public static readonly int walkHash = Animator.StringToHash("Walk");
    public DogPatrolState(DogStateMachine stateMachine) : base(stateMachine) { }

    private int checkCount = 120;
    private int currentCheckCount = 0;
    public override void Enter()
    {
        currentCheckCount = 0;
        stateMachine.TriggerPatrolCounter(true);
        stateMachine.RandomizePatrolTarget();
        stateMachine.movementHandler.SetTarget(stateMachine.patrolTarget);
        stateMachine.movementHandler.SetRunning(false);
        stateMachine.movementHandler.SetBreakDistance(stateMachine.patrolBreakDistance);
        stateMachine.movementHandler.MoveTowardsTarget();
        stateMachine.animator.CrossFadeInFixedTime(walkHash, 0.1f);
    }
    public override void Tick(float deltaTime)
    {
        currentCheckCount++;

        if (currentCheckCount == checkCount)
        {
            //MailMan
            stateMachine.presenceChecker.SearchForMailMan();
            MailManController mailManController = stateMachine.presenceChecker.GetCurrentMailMan();
            if (mailManController != null)
            {
                stateMachine.ChangeToInspectState();
                currentCheckCount = 0;
                return;
            }

            //Player
            stateMachine.presenceChecker.SearchForPlayer();
            var player = stateMachine.presenceChecker.GetCurrentCarriable();
            if (player != null)
            {
                stateMachine.ChangeToInspectState();
                currentCheckCount = 0;
                return;
            }

            currentCheckCount = 0;
        }

        if (stateMachine.movementHandler.IsMoving()) return;

        stateMachine.ChangeToInspectState();

    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {

    }
}
