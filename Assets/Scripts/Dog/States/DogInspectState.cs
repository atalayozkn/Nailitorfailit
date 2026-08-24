using UnityEngine;

public class DogInpectState : DogBaseState
{
    private enum ResultType
    {
        MailMan,
        Player,
        Grabbable,
        Empty
    }

    public static readonly int inspectHash = Animator.StringToHash("Inspect");
    public DogInpectState(DogStateMachine stateMachine) : base(stateMachine) { }

    private float counter;
    private float animDuration = 3.0f;
    private Transform currentTarget;
    private ResultType currentResult;
    public override void Enter()
    {
        counter = 0f;
        currentResult = ResultType.Empty;
        stateMachine.animator.CrossFadeInFixedTime(inspectHash, 0.1f);

        //MailMan
        stateMachine.presenceChecker.SearchForMailMan();
        var mailMan = stateMachine.presenceChecker.GetCurrentMailMan();

        if (mailMan != null)
        {
            currentTarget = stateMachine.presenceChecker.GetCurrentMailMan().transform;
        }
        if (currentTarget != null)
        {
            currentResult = ResultType.MailMan;
            return;
        }

        //Player
        stateMachine.presenceChecker.SearchForPlayer();
        var player = stateMachine.presenceChecker.GetCurrentCarriable();
        if (player != null)
        {
            currentTarget = player.transform;
        }
        if (currentTarget != null)
        {
            currentResult = ResultType.Player;
            return;
        }

        //Carriable
        stateMachine.presenceChecker.SearchForCarriable();
        var carriable = stateMachine.presenceChecker.GetCurrentCarriable();
        if (carriable != null)
        {
            currentTarget = carriable.transform;
        }
        if (currentTarget != null)
        {
            currentResult = ResultType.Grabbable;
            return;
        }

    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter > animDuration)
        {
            if (currentResult == ResultType.MailMan && stateMachine.favorController.GetPercentFavor() < 75)
            {
                stateMachine.SetChaseTarget(currentTarget);
                stateMachine.ChangeToChaseState();
                return;
            }
            if (currentResult == ResultType.Player && stateMachine.favorController.GetPercentFavor() < 60)
            {
                stateMachine.SetPlayTarget(currentTarget);
                stateMachine.ChangeToPlayState();
                return;
            }
            if (currentResult == ResultType.Grabbable)
            {
                stateMachine.SetPlayTarget(currentTarget);
                stateMachine.ChangeToPlayState();
                return;
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
