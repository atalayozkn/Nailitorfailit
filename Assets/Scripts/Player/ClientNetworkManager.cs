using UnityEngine;
using Mirror;
using PlayerScripts;

public class ClientNetworkController : NetworkBehaviour
{
    [SerializeField] private PlayerMove m_PlayerMove;
    [SerializeField] private PlayerCarry m_PlayerCarry;

    private void Awake()
    {
        m_PlayerMove.enabled = false;
        m_PlayerCarry.enabled = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isOwned)
        {
            m_PlayerCarry.enabled = true;
            m_PlayerMove.enabled = true;
        }
    }
}