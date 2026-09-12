using UnityEngine;
public class PlayerEmoteState : PlayerBaseState
{
    public PlayerEmoteState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    private static readonly int throwHash = Animator.StringToHash("Throw");
    private static readonly int danceHash = Animator.StringToHash("Dance");
    private static readonly int searchHash = Animator.StringToHash("Search");
    private static readonly int Emote04 = Animator.StringToHash("Something");
    private static readonly int Emote05 = Animator.StringToHash("Something");

    private int activeIndex;
    private float counter = 0f;
    private float actionTimer;
    private float animationTimer;
    private bool actionPerformed = false;
    public override void Enter()
    {
        stateMachine.movementHandler.SetActivity(false);

        activeIndex = stateMachine.activeEmoteIndex;
        switch (activeIndex)
        {
            case 0: //Throw Index
                stateMachine.animator.CrossFadeInFixedTime(throwHash, 0f);
                actionTimer = 1.15f;
                animationTimer = 3.3f;
                break;
            case 1: //Dance Index
                stateMachine.animator.CrossFadeInFixedTime(danceHash, 0f);
                actionTimer = 0f;
                animationTimer = 5f;
                break;
            case 2:
                stateMachine.animator.CrossFadeInFixedTime(searchHash, 0f);
                actionTimer = 0f;
                animationTimer = 5f;
                break;
            case 3:
                break;
            default:
                break;
        }
    }
    public override void Tick(float deltaTime)
    {
        counter += deltaTime;

        if (counter >= actionTimer && !actionPerformed)
        {
            actionPerformed = true;

            switch (activeIndex)
            {
                case 0:
                    var carriable = stateMachine.interactionHandler.GetCurrentCarriable();
                    if (carriable != null) carriable.OnThrow(1f);
                    break;
                case 1: //Do nothing
                    break;
                case 2: //Do nothing
                    break;
                case 3: //Do nothing
                    break;
                default: //Do nothing
                    break;
            }
        }

        if (counter >= animationTimer)
        {
            stateMachine.ChangeToIdleState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
        stateMachine.movementHandler.SetActivity(true);
    }
}
