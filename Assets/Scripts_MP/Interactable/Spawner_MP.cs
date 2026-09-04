
using Interactions.Networking;
using Mirror;

public class Spawner_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private ObjectSpawner spawner;

    private void Awake()
    {
        spawner = GetComponent<ObjectSpawner>();
    }

    public void RequestInteract() => RequestSpawn();

    public void RequestSpawn()
    {
        if (isServer) ServerSpawn();
        else CmdRequestSpawn();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestSpawn() => ServerSpawn();

    [Server]
    private void ServerSpawn() => spawner.OnInteract();
}
