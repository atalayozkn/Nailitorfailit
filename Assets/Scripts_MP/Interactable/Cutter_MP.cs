using Interactions.Networking;
using Mirror;
using UnityEngine;

public class Cutter_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Cutter cutter;
    private Generator_Prototype generator;

    // Generator fuel lives on the server (refuel + cutting run there); mirror it so every fuel gauge matches.
    [SyncVar(hook = nameof(OnEnergyChanged))] private float generatorEnergy = -1f;
    private float nextEnergyCheck;

    private void Awake()
    {
        cutter = GetComponent<Cutter>();
        generator = GetComponentInChildren<Generator_Prototype>(true);
    }

    [ServerCallback]
    private void Update()
    {
        if (generator == null || Time.time < nextEnergyCheck) return;
        nextEnergyCheck = Time.time + 0.2f;

        float energy = generator.GetEnergy();
        if (!Mathf.Approximately(energy, generatorEnergy)) generatorEnergy = energy;
    }

    private void OnEnergyChanged(float oldValue, float newValue)
    {
        if (isServer || generator == null || newValue < 0f) return;
        generator.ApplyNetworkedEnergy(newValue);
    }

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {

        bool usedGenerator = interactable is Generator_Prototype;

        if (isServer) ServerHandle(requester, usedGenerator);
        else CmdRequestInteract(requester, usedGenerator);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester, bool usedGenerator, NetworkConnectionToClient sender = null)
    {
        // Act for the sender's own player, never for an identity the client claims.
        if (sender != null && sender.identity != null) requester = sender.identity;
        ServerHandle(requester, usedGenerator);
    }

    [Server]
    private void ServerHandle(NetworkIdentity requester, bool usedGenerator)
    {
        InteractionDebug.Log($"Cutter_MP server handle: requester={requester?.name ?? "host"} " +
                             $"generator={usedGenerator} cutter={(cutter != null)}");

        if (usedGenerator)
        {
            if (generator == null)
            {
                Debug.LogError("Cutter_MP: no Generator_Prototype found in children.", this);
                return;
            }

            generator.ServerInteract(requester);
            return;
        }

        if (cutter == null)
        {
            Debug.LogError("Cutter_MP: no Cutter found on this object.", this);
            return;
        }

        float before = cutter.CutterProgress();
        cutter.ServerInteract(requester);
        float after = cutter.CutterProgress();

        // Progress / effects ran on the server only, so other players never saw the cutter work.
        if (!Mathf.Approximately(before, after)) RpcCutterFeedback(after, after > before);
    }

    [ClientRpc]
    private void RpcCutterFeedback(float normalized, bool cut)
    {
        if (cutter == null) return;

        if (!isServer) cutter.ApplyCutterProgress(normalized);
        if (cut) VfxManager.Play(VfxId.CutterCutting, cutter.CutterEffectPoint());
    }
}
