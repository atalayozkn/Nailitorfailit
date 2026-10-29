using Interactions.Networking;
using Mirror;
using UnityEngine;

public class Mixer_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Mixer mixer;
    private MixerHandle handle;
    private Generator_Prototype generator;

    private void Awake()
    {
        mixer = GetComponentInChildren<Mixer>();
        handle = GetComponentInChildren<MixerHandle>();
        generator = GetComponentInChildren<Generator_Prototype>(true);
    }

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {

        bool usedCrank = interactable is MixerHandle;
        bool usedGenerator = interactable is Generator_Prototype;

        if (isServer) ServerHandle(requester, usedCrank, usedGenerator);
        else CmdRequestInteract(requester, usedCrank, usedGenerator);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester, bool usedCrank, bool usedGenerator) =>
        ServerHandle(requester, usedCrank, usedGenerator);

    [Server]
    private void ServerHandle(NetworkIdentity requester, bool usedCrank, bool usedGenerator)
    {
        if (usedGenerator)
        {
            if (generator == null)
            {
                Debug.LogError("Mixer_MP: no Generator_Prototype found in children.", this);
                return;
            }

            generator.ServerInteract(requester);
            return;
        }

        if (usedCrank)
        {
            if (handle == null)
            {
                Debug.LogError("Mixer_MP: no MixerHandle found in children.", this);
                return;
            }

            mixer.ServerCompare(requester);
            return;
        }

        if (mixer == null)
        {
            Debug.LogError("Mixer_MP: no Mixer found in children.", this);
            return;
        }

        mixer.ServerInteract(requester);
    }
}
