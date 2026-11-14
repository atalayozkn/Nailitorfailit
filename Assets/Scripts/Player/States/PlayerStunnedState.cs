using ItemScript;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerStunnedState : PlayerBaseState
{
    private float counterTime;
    private GameObject stunEffect;
    private float stunDuration = 3f;
    private PlayerActiveRagdoll activeRagdoll;
    public PlayerStunnedState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {
        stunEffect = VfxManager.PlayLoop(VfxId.PlayerStun, stateMachine.transform);
        stateMachine.onStunStartEvent?.Invoke();
        stateMachine.UpdateMoveDirection();

        // A hit knocks the held item loose, otherwise it stays glued to the ragdoll's hand (oil bug).
        stateMachine.interactionHandler.ThrowHeldObject();

        //Stopping Inputs
        stateMachine.movementHandler.SetActivity(false);
        stateMachine.interactionHandler.SetActivity(false);
        counterTime = 0;

        // Active ragdoll (test prefab only): the body is already physical, just let go of the muscles.
        // The Animator keeps running so the stand-up can blend from the lying pose.
        activeRagdoll = stateMachine.GetComponent<PlayerActiveRagdoll>();
        if (activeRagdoll != null && activeRagdoll.enabled)
        {
            activeRagdoll.EnterLimp();
            return;
        }
        activeRagdoll = null;

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
        VfxManager.Stop(stunEffect);
        stateMachine.onStunEndEvent?.Invoke();

        if (activeRagdoll != null)
        {
            // Stand-up blend (or instant recover if the next state isn't Stand) is handled by the component.
            activeRagdoll.ExitLimp();
            stateMachine.movementHandler.SetActivity(true);
            stateMachine.interactionHandler.SetActivity(true);
            return;
        }

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
