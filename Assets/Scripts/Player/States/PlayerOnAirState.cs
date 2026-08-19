using UnityEngine;

public class PlayerOnAirState : PlayerBaseState
{
    public static readonly int jumpStartHash = Animator.StringToHash("jumpStart");
    public static readonly int onAirHash = Animator.StringToHash("onAir");
    public static readonly int onLandHash = Animator.StringToHash("onLand");
    private enum AirPhase
    {
        JumpStart,
        OnAir,
        Landing
    }

    private AirPhase currentPhase;
    private float counterTime;

    public PlayerOnAirState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        counterTime = 0f;
        currentPhase = AirPhase.JumpStart;
        stateMachine.animator.CrossFadeInFixedTime(jumpStartHash, 0.1f);
    }

    public override void Tick(float deltaTime)
    {
        counterTime += deltaTime;

        switch (currentPhase)
        {
            case AirPhase.JumpStart:
                HandleJumpStartPhase();
                break;

            case AirPhase.OnAir:
                HandleOnAirPhase();
                break;

            case AirPhase.Landing:
                HandleLandingPhase();
                break;
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {
       
    }

    public override void Exit()
    {
        stateMachine.movementHandler.SetJumping(false);
    }
    private void HandleJumpStartPhase()
    {
        if (counterTime < 0.15f) return;
        counterTime = 0f;
        currentPhase = AirPhase.OnAir;
        stateMachine.animator.CrossFadeInFixedTime(onAirHash, 0f);
    }
    private void HandleOnAirPhase()
    {
        if (!stateMachine.movementHandler.IsGrounded()) return;
        counterTime = 0f;
        currentPhase = AirPhase.Landing;
        stateMachine.animator.CrossFadeInFixedTime(onLandHash, 0f);
    }
    private void HandleLandingPhase()
    {
        if (counterTime < 0.1f) return;
        if (stateMachine.movementHandler.IsMoving()) stateMachine.ChangeToNavigationState();
        else stateMachine.ChangeToIdleState();
    }
}