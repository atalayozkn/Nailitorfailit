using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class ElectrocuteHelper : MonoBehaviour
{
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private UnityEvent onElectrifyEvent;

    private bool isElectrified;
    private void OnEnable()
    {
        SetActivity(false);
    }
    private void OnTriggerEnter(Collider other)
    {
        TryElectrocute(other);
    }
    // A tile can become electrified while the player is already standing on it.
    private void OnTriggerStay(Collider other)
    {
        TryElectrocute(other);
    }
    private void TryElectrocute(Collider other)
    {
        if (!isElectrified) return;
        // Multiplayer keeps its current behaviour, electrified water isn't synced there.
        if (NetworkModeHelper.IsNetworked) return;
        if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return;
        if (other.gameObject.TryGetComponent<PlayerStateMachine>(out var stateMachine))
        {
            stateMachine.SetDeathReason(DeathReason.Electricty);
            stateMachine.ChangeToDeadState();
        }
    }
    public void SetActivity(bool condition)
    {
        if (condition != isElectrified) isElectrified = condition;
        if (condition) onElectrifyEvent?.Invoke();
    }
}
