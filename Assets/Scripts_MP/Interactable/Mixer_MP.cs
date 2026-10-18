using Interactions.Networking;
using Mirror;
using UnityEngine;

/// <summary>
/// One proxy for the whole mixer station (it must live on the root, because that is
/// where the NetworkIdentity is). It decides what the interaction means from the
/// interactable the player actually looked at:
///   - the crank (MixerHandle)  -> compare the formula / mix
///   - the station itself       -> place the carried ingredient into the next slot
///
/// Both run on the HOST only, and every state change (item into a slot, item
/// consumed, product spawned) is replicated through CarriableObject_MP /
/// NetworkSpawnSelf_MP, so all clients stay in step.
///
/// (The file used to be MixerHandle_MP.cs; the class was renamed because it now
/// handles both interactions. The script GUID is unchanged, so the prefabs that
/// reference it keep working - only the class-name string in the prefab was updated.)
/// </summary>
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
        // Component references cannot travel in a Command, so the decision is made
        // here (where the real interactable is known) and only the answer is sent.
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
