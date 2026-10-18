using Interactions.Networking;
using ItemScript;
using Mirror;
using UnityEngine;

/// <summary>
/// Station-root proxy for a construction wall. Two different interactables live on
/// this prefab:
///   - the Constructor itself (build the wall from a processed material),
///   - the wall TILES (WoodTile / GlassTile / BrickTile - progress a partially built
///     wall until it completes).
///
/// The wrapper searches proxies upwards, so a tile press used to reach THIS proxy and
/// was handled as "build the wall" (wrong). The interactable component tells them
/// apart, and tile work is replicated as a completion state through a ClientRpc.
///
/// The file used to be named Constructor_MP.cs; the GUID is unchanged.
/// </summary>
public class Constructor_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Constructor constructor;

    private void Awake()
    {
        constructor = GetComponent<Constructor>();
    }

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        bool isTile = IsTile(interactable);
        string tileName = isTile && interactable != null ? interactable.gameObject.name : null;

        if (isServer) ServerHandle(requester, isTile, tileName);
        else CmdRequestInteract(requester, isTile, tileName);
    }

    /// <summary>Keep the old call shape for stray references.</summary>
    public void RequestBuild() => RequestBuild(null);

    public void RequestBuild(NetworkIdentity requester)
    {
        if (isServer) ServerHandle(requester, false, null);
        else CmdRequestInteract(requester, false, null);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester, bool isTile, string tileName) =>
        ServerHandle(requester, isTile, tileName);

    [Server]
    private void ServerHandle(NetworkIdentity requester, bool isTile, string tileName)
    {
        InteractionDebug.Log($"Constructor_MP: requester={requester?.name ?? "host"} " +
                             $"tile={isTile} tileName='{tileName}'");

        if (isTile)
        {
            Component tile = FindTile(tileName);

            if (tile == null)
            {
                Debug.LogError($"Constructor_MP: tile '{tileName}' not found under '{name}'.", this);
                return;
            }

            DispatchTileServerInteract(tile, requester);

            // Keep the clients' progress UI/visual in step with the host (the host is
            // the authority on the process itself).
            RpcApplyTileProgress(tileName, GetTileProgress(tile));
            return;
        }

        if (constructor == null)
        {
            Debug.LogError("Constructor_MP: no Constructor on this object.", this);
            return;
        }

        if (constructor.IsBuilt()) return;

        constructor.ServerInteract(requester);
    }

    [ClientRpc]
    private void RpcApplyTileProgress(string tileName, float normalized)
    {
        if (isServer) return;   // the host already has the real state

        Component tile = FindTile(tileName);
        if (tile == null) return;

        ApplyTileProgress(tile, normalized);
    }

    [ClientRpc]
    private void RpcApplyTileCompleted(string tileName)
    {
        if (isServer) return;   // the host already applied it locally

        Component tile = FindTile(tileName);
        if (tile == null) return;

        ApplyTileCompletedState(tile);

        InteractionDebug.Log($"Constructor_MP: tile '{tileName}' marked completed on this client.");
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsTile(Component interactable) =>
        interactable is WoodTile || interactable is GlassTile || interactable is BrickTile;

    private Component FindTile(string tileName)
    {
        foreach (Component candidate in GetComponentsInChildren<Component>(true))
        {
            if (!IsTile(candidate)) continue;
            if (!string.IsNullOrEmpty(tileName) && candidate.gameObject.name != tileName) continue;
            return candidate;
        }

        return null;
    }

    private void DispatchTileServerInteract(Component tile, NetworkIdentity requester)
    {
        switch (tile)
        {
            case WoodTile wood:
                wood.ServerInteract(requester);
                if (wood.GetCurrentPhase() == ConstructionPhase.Complete)
                    RpcApplyTileCompleted(wood.gameObject.name);
                break;

            case GlassTile glass:
                glass.ServerInteract(requester);
                if (glass.GetCurrentPhase() == ConstructionPhase.Complete)
                    RpcApplyTileCompleted(glass.gameObject.name);
                break;

            case BrickTile brick:
                brick.ServerInteract(requester);
                if (brick.GetCurrentPhase() == ConstructionPhase.Complete)
                    RpcApplyTileCompleted(brick.gameObject.name);
                break;
        }
    }

    private static float GetTileProgress(Component tile)
    {
        switch (tile)
        {
            case WoodTile wood: return wood.GetProcessNormalized();
            case GlassTile glass: return glass.GetProcessNormalized();
            case BrickTile brick: return brick.GetProcessNormalized();
            default: return 0f;
        }
    }

    private static void ApplyTileProgress(Component tile, float normalized)
    {
        switch (tile)
        {
            case WoodTile wood: wood.ApplyNetworkedProgress(normalized); break;
            case GlassTile glass: glass.ApplyNetworkedProgress(normalized); break;
            case BrickTile brick: brick.ApplyNetworkedProgress(normalized); break;
        }
    }

    private static void ApplyTileCompletedState(Component tile)
    {
        switch (tile)
        {
            case WoodTile wood: wood.ApplyCompletedState(); break;
            case GlassTile glass: glass.ApplyCompletedState(); break;
            case BrickTile brick: brick.ApplyCompletedState(); break;
        }
    }
}
