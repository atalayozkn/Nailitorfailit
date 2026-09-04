
using Mirror;
using UnityEngine;

public class NetworkSpawnSelf_MP : NetworkBehaviour
{
    private void Start()
    {
        if (!NetworkServer.active) return;

        NetworkIdentity identity = GetComponent<NetworkIdentity>();
        if (identity == null || identity.netId != 0) return;

        NetworkServer.Spawn(gameObject);
    }
}
