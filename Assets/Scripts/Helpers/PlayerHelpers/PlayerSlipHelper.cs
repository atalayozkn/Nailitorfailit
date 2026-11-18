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

        // Multiplayer: the player who stepped in it slips, and only on their own machine (the first state machine found
        // in the scene is not necessarily that player; the other machines follow the owner's state).
        if (NetworkModeHelper.IsNetworked)
        {
            PlayerStateMachine stepped = other.GetComponentInParent<PlayerStateMachine>();
            if (stepped == null) return;

            var net = stepped.GetComponent<PlayerScripts.PlayerNetwork_MP>();
            if (net != null && !net.isOwned) return;

            stepped.ChangeToSlippingState();
            return;
        }

        stateMachine.ChangeToSlippingState();
    }
    public void SetActivity(bool condition)
    {
        if (condition == isActive) return;
        isActive = condition;
    }
}