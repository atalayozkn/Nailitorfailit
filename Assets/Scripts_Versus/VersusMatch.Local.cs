using System.Collections.Generic;
using Interactions;
using ItemScript;
using Mirror;
using PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;

// The part of the versus referee that serves the player on this machine:
// - switches the singleplayer comfort features back on for this scene (they are off in co-op multiplayer),
// - knows what belongs to whom, so the other player's building, stations and dog do nothing for you,
// - feeds the look-at panel, the inventory keys and hazard repairs through the network.
public partial class VersusMatch
{
    private const string NotYours = "Not yours";

    [Header("Local player")]
    [Tooltip("Input actions asset of the player (for the inventory slot keys).")]
    [SerializeField] private InputActionAsset controls;

    private readonly Dictionary<Component, int> ownerCache = new Dictionary<Component, int>();
    private PlayerInteractionHandler uiBuiltFor;
    private InputAction[] slotActions;
    private InputAction nextSlotAction;
    private InputAction previousSlotAction;

    private void EnableSingleplayerFeel()
    {
        NetworkModeHelper.SingleplayerFeel = true;
        PlayerInteractionNetwork_MP.InteractOverride = OnLocalInteract;
        PlayerInteractionNetwork_MP.HoverFilter = IsForeign;
        BlueprintVisual.IsForeign = IsForeign;
        LookAtInfoUI.Override = LocalLookAt;
    }

    private void DisableSingleplayerFeel()
    {
        NetworkModeHelper.SingleplayerFeel = false;
        PlayerInteractionNetwork_MP.InteractOverride = null;
        PlayerInteractionNetwork_MP.HoverFilter = null;
        BlueprintVisual.IsForeign = null;
        LookAtInfoUI.Override = null;
    }

    #region OWNERSHIP

    // True for things of the other player that the local player has no business with: their building slots and tiles,
    // their stations and generators, their dog, bowl and mailbox. Loose items are never foreign, so stealing stays possible.
    public bool IsForeign(Component target)
    {
        if (!Networked || target == null) return false;

        int local = LocalSideIndex();
        if (local < 0) return false;

        int owner = OwnerSide(target);
        return owner >= 0 && owner != local;
    }

    private int OwnerSide(Component target)
    {
        if (ownerCache.TryGetValue(target, out int cached)) return cached;
        if (target.GetComponentInParent<CarriableObject_SP>() != null) return -1;

        Transform transform = target.transform;

        for (int i = 0; i < sides.Length; i++)
        {
            Side side = sides[i];
            bool inBuilding = side.buildingRoot != null && transform.IsChildOf(side.buildingRoot);
            bool inStations = side.stationsRoot != null && transform.IsChildOf(side.stationsRoot);
            if (!inBuilding && !inStations) continue;

            ownerCache[target] = i;
            return i;
        }

        // Part of a player's own package (dog, bowl, mailbox). Sides can change hands, so this is not cached.
        NetworkIdentity identity = transform.root.GetComponent<NetworkIdentity>();
        return identity != null ? SideIndexOf(identity.netId) : -1;
    }

    #endregion

    #region INTERACTION

    // Runs on the local player's interact press, before the multiplayer stack routes it.
    private bool OnLocalInteract(PlayerInteractionNetwork_MP player, Component target)
    {
        if (IsForeign(target))
        {
            WarningManager.Show(NotYours);
            return true;
        }

        if (target is TelegraphedHazard hazard)
        {
            for (int i = 0; i < sides.Length; i++)
            {
                int socket = sides[i].sockets != null ? System.Array.IndexOf(sides[i].sockets, hazard) : -1;
                if (socket < 0) continue;

                NetworkClient.Send(new VersusRepairMessage { side = i, socket = socket });
                break;
            }
            return true;
        }

        return false;
    }

    private void OnServerRepair(NetworkConnectionToClient conn, VersusRepairMessage message)
    {
        NetworkServer.SendToAll(message, Channels.Reliable, true);
    }

    // The repair lands on every machine, so the warning stops for both players.
    private void ApplyRepair(VersusRepairMessage message)
    {
        if (message.side < 0 || message.side >= sides.Length) return;

        ElectricalHazard_Prototype[] sockets = sides[message.side].sockets;
        if (sockets == null || message.socket < 0 || message.socket >= sockets.Length || sockets[message.socket] == null) return;

        sockets[message.socket].OnInteract();
    }

    // What the look-at panel shows for the local player. The multiplayer stack does its own targeting,
    // so the panel reads the target from there instead of the (disabled) singleplayer handler.
    private bool LocalLookAt(out LookAtInfo info)
    {
        info = default;

        PlayerInteractionHandler handler = NetworkModeHelper.LocalHandler();
        if (handler == null) return false;
        if (!Networked) return handler.TryGetLookAtInfo(out info);

        if (!handler.TryGetComponent(out PlayerInteractionNetwork_MP network)) return false;
        if (handler.TryGetComponent(out PlayerStateMachine state) && state.currentPlayerState == PlayerStates.Dead) return false;

        CarriableObject_SP carried = handler.GetCurrentCarriable();

        if (network.CurrentInteractable is Component target && target != null)
        {
            ILookAtInfo provider = target as ILookAtInfo;
            CarriableObject_SP held = network.CurrentInteractableType == InteractableType.Grabbable ? null : carried;

            if (IsForeign(target))
            {
                string name = provider != null ? provider.GetLookAtInfo(held).Name : null;
                info = LookAtInfo.Blocked(string.IsNullOrEmpty(name) ? target.name : name, NotYours);
                return true;
            }

            if (provider == null) return false;

            info = provider.GetLookAtInfo(held);
            return !string.IsNullOrEmpty(info.Name);
        }

        if (carried == null) return false;

        info = LookAtInfo.Ready(LookAtText.ItemName(carried), "Drop");
        return true;
    }

    #endregion

    #region LOCAL UI AND INVENTORY

    private void LocalTick()
    {
        if (!Networked) return;

        PlayerInteractionHandler handler = NetworkModeHelper.LocalHandler();
        if (handler == null) return;

        // The singleplayer handler builds these in Start, which never runs in multiplayer (the handler is disabled there).
        if (handler != uiBuiltFor)
        {
            uiBuiltFor = handler;
            LookAtInfoUI.Create(handler);
            InventoryUI.Create(handler);
        }

        if (winner == NoWinner) HandleSlotKeys(handler);
    }

    private void HandleSlotKeys(PlayerInteractionHandler handler)
    {
        if (controls == null) return;

        if (slotActions == null)
        {
            slotActions = new InputAction[PlayerInteractionHandler.InventorySize];
            for (int i = 0; i < slotActions.Length; i++) slotActions[i] = Enabled(controls.FindAction("Slot" + (i + 1)));
            nextSlotAction = Enabled(controls.FindAction("NextSlot"));
            previousSlotAction = Enabled(controls.FindAction("PreviousSlot"));
        }

        int size = PlayerInteractionHandler.InventorySize;
        int active = handler.GetActiveSlot();
        int wanted = -1;

        for (int i = 0; i < slotActions.Length; i++)
        {
            if (slotActions[i] != null && slotActions[i].WasPressedThisFrame()) wanted = i;
        }

        if (wanted < 0 && nextSlotAction != null && nextSlotAction.WasPressedThisFrame()) wanted = (active + 1) % size;
        if (wanted < 0 && previousSlotAction != null && previousSlotAction.WasPressedThisFrame()) wanted = (active + size - 1) % size;

        if (wanted >= 0 && wanted != active) NetworkClient.Send(new VersusSlotMessage { slot = wanted });
    }

    private static InputAction Enabled(InputAction action)
    {
        if (action != null && !action.enabled) action.Enable();
        return action;
    }

    private void OnServerSlot(NetworkConnectionToClient conn, VersusSlotMessage message)
    {
        if (conn.identity == null) return;

        message.player = conn.identity.netId;
        NetworkServer.SendToAll(message, Channels.Reliable, true);
    }

    // Every machine switches the same slot on its copy of that player, so held and stowed items stay in step.
    private void ApplySlot(VersusSlotMessage message)
    {
        if (!NetworkClient.spawned.TryGetValue(message.player, out NetworkIdentity identity)) return;

        PlayerInteractionHandler handler = identity.GetComponentInChildren<PlayerInteractionHandler>(true);
        if (handler != null) handler.SelectSlot(message.slot);
    }

    #endregion
}
