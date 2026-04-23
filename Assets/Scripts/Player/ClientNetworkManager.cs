using UnityEngine;
using Mirror;
using PlayerScripts;

public class ClientNetworkController : NetworkBehaviour
{
    [SerializeField] private PlayerMove m_PlayerMove;
    //[SerializeField] private PlayerCarry m_PlayerCarry;

    private void Awake()
    {
        if (m_PlayerMove != null)
            m_PlayerMove.enabled = false;

        //if (m_PlayerCarry != null)
        //    m_PlayerCarry.enabled = false;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        m_PlayerMove.enabled = true;
        //m_PlayerCarry.enabled = true;
    }
}