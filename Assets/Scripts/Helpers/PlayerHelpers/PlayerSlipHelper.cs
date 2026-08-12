using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerSlipHelper : MonoBehaviour
{
    [SerializeField] private LayerMask slipperyMask;
    [SerializeField] private bool startActive = true;
    private PlayerStateMachine stateMachine;
    private bool isActive;

    private void Awake()
    {
        stateMachine = FindFirstObjectByType<PlayerStateMachine>();
        if (startActive) isActive = true;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if ((slipperyMask.value & (1 << other.gameObject.layer)) == 0) return;
        stateMachine.ChangeToSlippingState();
    }
    public void SetActivity(bool condition)
    {
        if (condition == isActive) return;
        isActive = condition;
    }
}