using ItemScript;
using NUnit.Framework.Interfaces;
using UnityEngine;

public class DogInpectState : DogBaseState
{
    public static readonly int inspectHash = Animator.StringToHash("Inspect");
    public DogInpectState(DogStateMachine stateMachine) : base(stateMachine) { }

    private float counter;
    private float animDuration = 3.0f;
    public override void Enter()
    {
        counter = 0f;
        stateMachine.animator.CrossFadeInFixedTime(inspectHash, 0.1f);
        stateMachine.InspectEnvironment();
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter > animDuration)
        {
            MailManController mailManController = stateMachine.presenceChecker.GetCurrentMailMan();
            
            if (mailManController != null)
            {
                stateMachine.SetChaseTarget(mailManController.transform);
                stateMachine.ChangeToChaseState();
                return;
            }

            if (stateMachine.presenceChecker.GetCurrentCarriable())
            {
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
