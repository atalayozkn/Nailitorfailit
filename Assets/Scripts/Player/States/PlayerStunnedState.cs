using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerStunnedState : PlayerBaseState
{
    private float counterTime;
    private float stunDuration = 3f;
    public PlayerStunnedState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stateMachine.onStunStartEvent?.Invoke();
        stateMachine.UpdateMoveDirection();

        //Stopping Inputs & Disabling Animator
        stateMachine.movementHandler.SetActivity(false);
        stateMachine.interactionHandler.SetActivity(false);
        stateMachine.animator.enabled = false;

        //Activation of Ragdoll
        foreach (var joint in stateMachine.ragdollJoints) joint.enableCollision = true;
        foreach (var col in stateMachine.ragdollColliders) col.enabled = true;
        foreach (var rb in stateMachine.ragdollRigidBodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.detectCollisions = true;
            rb.useGravity = true;
            rb.AddForce(stateMachine.moveDirection * 15.0f, ForceMode.Force);
        }

        counterTime = 0;
    }
    public override void Tick(float deltaTime)
    {
        counterTime += deltaTime;

        if (counterTime >= stunDuration)
        {
            stateMachine.ChangeToStandState();
        }
    }
    public override void FixedTick(float fixedDeltaTime)
    {

    }
    public override void Exit()
    {
        stateMachine.onStunEndEvent?.Invoke();

        //Disable of Ragdoll
        foreach (var joint in stateMachine.ragdollJoints) joint.enableCollision = false;
        foreach (var col in stateMachine.ragdollColliders) col.enabled = false;
        foreach (var rb in stateMachine.ragdollRigidBodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.detectCollisions = false;
            rb.useGravity = false;
        }

        stateMachine.animator.enabled = true;
        stateMachine.movementHandler.SetActivity(true);
        stateMachine.interactionHandler.SetActivity(true);
    }
}
