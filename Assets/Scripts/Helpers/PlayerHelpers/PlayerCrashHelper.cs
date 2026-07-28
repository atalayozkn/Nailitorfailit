using UnityEngine;

public class PlayerCrashHelper : MonoBehaviour
{
    [SerializeField] private LayerMask whatIsCrashable;
    [SerializeField] private PlayerStateMachine stateMachine;

    private bool isActive;

    private void OnEnable()
    {
        isActive = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive)
            return;

        if (((1 << collision.gameObject.layer) & whatIsCrashable) == 0)
            return;

        stateMachine.ChangeToStunnedState();
    }

    public void SetActivity(bool condition)
    {
        isActive = condition;
    }
}