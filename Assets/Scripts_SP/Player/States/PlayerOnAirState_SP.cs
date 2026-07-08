using UnityEngine;

public class PlayerOnAirState_SP : PlayerBaseState_SP
{
    public static readonly int jumpStartHash = Animator.StringToHash("jumpStart");
    public static readonly int onAirHash = Animator.StringToHash("onAir");
    public static readonly int onLandHash = Animator.StringToHash("onLand");

    private float jumpStartDuration;
    private float onLandDuration;

    private enum AirPhase
    {
        JumpStart,
        OnAir,
        Landing
    }

    private AirPhase currentPhase;
    private float counterTime;

    public PlayerOnAirState_SP(PlayerStateMachine_SP stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        jumpStartDuration = stateMachine.GetAnimDuration("jumpStart", 0.25f);
        onLandDuration = stateMachine.GetAnimDuration("onLand", 0.35f);

        counterTime = 0f;
        currentPhase = AirPhase.JumpStart;

        stateMachine.SetJumping(true);

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
        stateMachine.CheckGround();
    }

    public override void Exit()
    {
        stateMachine.SetJumping(false);
    }

    private void HandleJumpStartPhase()
    {
        if (counterTime < jumpStartDuration)
            return;

        counterTime = 0f;
        currentPhase = AirPhase.OnAir;

        stateMachine.animator.CrossFadeInFixedTime(onAirHash, 0.1f);
    }

    private void HandleOnAirPhase()
    {
        if (!stateMachine.IsGrounded)
            return;

        counterTime = 0f;
        currentPhase = AirPhase.Landing;

        stateMachine.animator.CrossFadeInFixedTime(onLandHash, 0.1f);
    }

    private void HandleLandingPhase()
    {
        if (counterTime < onLandDuration)
            return;

        stateMachine.SetJumping(false);

        if (stateMachine.isMoving)
            stateMachine.ForceSwitchToNavigationState();
        else
            stateMachine.ForceSwitchToIdleState();
    }
}