using System.Collections.Generic;
using Interactions.Networking;
using ItemScript;
using Mirror;
using UnityEngine;

// Server runs the SP logic (through SharedMpCompat); every result is mirrored to all machines:
//   placing a tile into the Constructor -> RpcApplyBuilt (blueprint off, model on, collider off, VFX)
//   nailing a tile                      -> RpcApplyTileProgress (progress bar + nail VFX)
//   finishing a tile                    -> RpcApplyTileCompleted (phase, layer, wetness, room counter, VFX)
// Tiles are addressed by their child-index path under this object, so equally named tiles can't collide.
public class Constructor_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Constructor constructor;

    private void Awake()
    {
        constructor = GetComponent<Constructor>();
    }

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        string tilePath = IsTile(interactable) ? PathTo(interactable.transform) : null;

        if (isServer) ServerHandle(requester, tilePath);
        else CmdRequestInteract(requester, tilePath);
    }

    public void RequestBuild() => RequestBuild(null);

    public void RequestBuild(NetworkIdentity requester)
    {
        if (isServer) ServerHandle(requester, null);
        else CmdRequestInteract(requester, null);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester, string tilePath, NetworkConnectionToClient sender = null)
    {
        if (requester != null && sender != null && requester.connectionToClient != sender) return;
        ServerHandle(requester, tilePath);
    }

    [Server]
    private void ServerHandle(NetworkIdentity requester, string tilePath)
    {
        InteractionDebug.Log($"Constructor_MP: requester={requester?.name ?? "host"} tile='{tilePath ?? "-"}'");

        if (tilePath != null)
        {
            Component tile = FindTile(tilePath);

            if (tile == null)
            {
                Debug.LogError($"Constructor_MP: tile '{tilePath}' not found under '{name}'.", this);
                return;
            }

            float before = GetTileProgress(tile);
            bool completed = DispatchTileServerInteract(tile, requester);
            float after = GetTileProgress(tile);

            if (completed) RpcApplyTileCompleted(tilePath);
            else if (!Mathf.Approximately(before, after)) RpcApplyTileProgress(tilePath, after);
            return;
        }

        if (constructor == null)
        {
            Debug.LogError("Constructor_MP: no Constructor on this object.", this);
            return;
        }

        if (constructor.IsBuilt()) return;

        constructor.ServerInteract(requester);

        if (constructor.IsBuilt()) RpcApplyBuilt((int)constructor.GetAcceptedType());
    }

    [ClientRpc]
    private void RpcApplyBuilt(int type)
    {
        if (constructor == null) return;

        if (!isServer) MpSharedCompat.ApplyConstructorBuiltOnClient(constructor, (CarriableType)type);

        VfxManager.Play(VfxId.TilePlaced, transform.position);
    }

    [ClientRpc]
    private void RpcApplyTileProgress(string tilePath, float normalized)
    {
        Component tile = FindTile(tilePath);
        if (tile == null) return;

        if (!isServer) ApplyTileProgress(tile, normalized);

        VfxManager.Play(VfxId.TileNail, tile.transform.position);
    }

    [ClientRpc]
    private void RpcApplyTileCompleted(string tilePath)
    {
        Component tile = FindTile(tilePath);
        if (tile == null) return;

        if (!isServer) MpSharedCompat.ApplyTileCompletedOnClient((MonoBehaviour)tile);

        VfxManager.Play(VfxId.TileFinished, tile.transform.position);

        InteractionDebug.Log($"Constructor_MP: tile '{tilePath}' completed ({(isServer ? "server" : "client")}).");
    }

    private static bool IsTile(Component interactable) =>
        interactable is WoodTile || interactable is GlassTile || interactable is BrickTile;

    private string PathTo(Transform target)
    {
        var indices = new List<int>();

        for (Transform t = target; t != null && t != transform; t = t.parent) indices.Add(t.GetSiblingIndex());

        indices.Reverse();
        return string.Join("/", indices);
    }

    private Component FindTile(string tilePath)
    {
        if (string.IsNullOrEmpty(tilePath)) return null;

        Transform current = transform;

        foreach (string part in tilePath.Split('/'))
        {
            if (!int.TryParse(part, out int index) || index < 0 || index >= current.childCount) return null;
            current = current.GetChild(index);
        }

        foreach (Component candidate in current.GetComponents<Component>())
        {
            if (IsTile(candidate)) return candidate;
        }

        return null;
    }

    // Returns true when this interaction finished the tile.
    private static bool DispatchTileServerInteract(Component tile, NetworkIdentity requester)
    {
        switch (tile)
        {
            case WoodTile wood:
                if (wood.GetCurrentPhase() == ConstructionPhase.Complete) return false;
                wood.ServerInteract(requester);
                return wood.GetCurrentPhase() == ConstructionPhase.Complete;

            case GlassTile glass:
                if (glass.GetCurrentPhase() == ConstructionPhase.Complete) return false;
                glass.ServerInteract(requester);
                return glass.GetCurrentPhase() == ConstructionPhase.Complete;

            case BrickTile brick:
                if (brick.GetCurrentPhase() == ConstructionPhase.Complete) return false;
                brick.ServerInteract(requester);
                return brick.GetCurrentPhase() == ConstructionPhase.Complete;
        }

        return false;
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
}
