using ItemScript;
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
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.detectCollisions = true;
            rb.useGravity = true;
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

        foreach (var col in stateMachine.ragdollColliders)
        {
            col.enabled = false;
        }

        foreach (var joint in stateMachine.ragdollJoints)
        {
            joint.enableCollision = false;
        }

        // Stop ragdoll physics
        foreach (var rb in stateMachine.ragdollRigidBodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.useGravity = false;
        }

        stateMachine.rHandTransform.localPosition = stateMachine.rightHandLocalPosition;
        stateMachine.rHandTransform.localRotation = stateMachine.rightHandLocalRotation;

        stateMachine.lHandTransform.localPosition = stateMachine.leftHandLocalPosition;
        stateMachine.lHandTransform.localRotation = stateMachine.leftHandLocalRotation;

        // Restore animation
        stateMachine.animator.enabled = true;
        stateMachine.animator.Rebind();
        stateMachine.animator.Update(0f);

        stateMachine.movementHandler.SetActivity(true);
        stateMachine.interactionHandler.SetActivity(true);
    }
}
