using Unity.Multiplayer.Tools.NetStatsMonitor;
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
        stateMachine.favorController.GainFavor(stateMachine.perPetFavorGain);
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
