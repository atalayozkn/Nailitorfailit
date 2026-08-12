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
        if (!isElectrified) return;
        if (other.gameObject.layer != targetMask) return;
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
