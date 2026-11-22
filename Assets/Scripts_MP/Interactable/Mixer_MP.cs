using Interactions.Networking;
using Mirror;
using UnityEngine;

public class Mixer_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Mixer mixer;
    private MixerHandle handle;
    private Generator_Prototype generator;

    // What the three slots show (CarriableType as int, -1 = empty). The letters are a local UI, so every machine
    // updates its own copy when these change (also for a player who joins later).
    [SyncVar(hook = nameof(OnSlotChanged))] private int slot1 = -1;
    [SyncVar(hook = nameof(OnSlotChanged))] private int slot2 = -1;
    [SyncVar(hook = nameof(OnSlotChanged))] private int slot3 = -1;

    // The generator fuel lives on the server (refuel and mixing run there). Mirrored here so the fuel bar on the mixer
    // moves on every machine, same as the cutter's.
    [SyncVar(hook = nameof(OnEnergyChanged))] private float generatorEnergy = -1f;
    private float nextEnergyCheck;

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
        if (usedCrank && mixer != null) mixer.NotifyHandleUsed();

        if (isServer) ServerHandle(requester, usedCrank, usedGenerator);
        else CmdRequestInteract(requester, usedCrank, usedGenerator);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester, bool usedCrank, bool usedGenerator, NetworkConnectionToClient sender = null)
    {
        // Act for the sender's own player, never for an identity the client claims.
        if (sender != null && sender.identity != null) requester = sender.identity;
        ServerHandle(requester, usedCrank, usedGenerator);
    }

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

    [Server]
    public void ServerSetSlot(int index, CarriableType type)
    {
        if (index == 1) slot1 = (int)type;
        else if (index == 2) slot2 = (int)type;
        else slot3 = (int)type;
    }

    [Server]
    public void ServerClearSlots()
    {
        slot1 = slot2 = slot3 = -1;
    }

    public override void OnStartClient()
    {
        ApplySlots();
        OnEnergyChanged(0f, generatorEnergy);
    }

    private void OnSlotChanged(int oldValue, int newValue) => ApplySlots();

    private void ApplySlots()
    {
        if (isServer) return; // the server (host) already updated its UI directly

        if (mixer != null) mixer.SetRemoteFull(slot1 >= 0 && slot2 >= 0 && slot3 >= 0);

        MixerUIHelper ui = GetComponentInChildren<MixerUIHelper>(true);
        if (ui == null) return;

        ui.ClearImages();
        MpMixerIcons_MP.ClearAll(ui);
        if (slot1 >= 0) { ui.DisplayItem(1, (CarriableType)slot1); MpMixerIcons_MP.Show(ui, 1, (CarriableType)slot1); }
        if (slot2 >= 0) { ui.DisplayItem(2, (CarriableType)slot2); MpMixerIcons_MP.Show(ui, 2, (CarriableType)slot2); }
        if (slot3 >= 0) { ui.DisplayItem(3, (CarriableType)slot3); MpMixerIcons_MP.Show(ui, 3, (CarriableType)slot3); }
    }
}
