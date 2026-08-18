using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerSlipHelper : MonoBehaviour
{
    [SerializeField] private LayerMask slipperyMask;

    private PlayerStateMachine stateMachine;

    private void Awake()
    {
        stateMachine = FindFirstObjectByType<PlayerStateMachine>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & slipperyMask) != 0)
        {
            stateMachine.ChangeToSlippingState();
        }
    }
}