using System.Collections;
using Interactions.Networking;
using Mirror;
using UnityEngine;

// One networked tile (wood / brick / glass nailed down inside a Constructor). The tile owns its own synced state, so
// there is no tile address to resolve and no per-constructor table to keep in step:
//   progress = how far it is nailed (0..1),  complete = it is finished.
// The server runs the SP logic through SharedMpCompat and writes the result into these two values; every client
// applies them to its local copy of the tile (progress bar, phase, layer, wetness, room counter).
// Tiles sit inside inactive objects until the Constructor is built, and the SP tile resets itself every time it is
// switched on, so the state is applied again on enable.
public class Tile_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    [SyncVar(hook = nameof(OnProgressChanged))] private float progress;
    [SyncVar(hook = nameof(OnCompleteChanged))] private bool complete;

    private MonoBehaviour tile;
    private float nextServerCheckAt;

    private MonoBehaviour Tile
    {
        get
        {
            if (tile != null) return tile;
            tile = GetComponent<WoodTile>();
            if (tile == null) tile = GetComponent<GlassTile>();
            if (tile == null) tile = GetComponent<BrickTile>();
            return tile;
        }
    }

    // Called by MpLevelInstaller on a Constructor, BEFORE its NetworkIdentity is added (the identity collects its
    // NetworkBehaviours when it is created).
    public static int AttachTo(GameObject constructorRoot)
    {
        int added = 0;

        foreach (MonoBehaviour candidate in constructorRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(candidate is WoodTile || candidate is GlassTile || candidate is BrickTile)) continue;
            if (candidate.GetComponent<Tile_MP>() != null) continue;

            candidate.gameObject.AddComponent<Tile_MP>();
            added++;
        }

        return added;
    }

    // ---- interaction -------------------------------------------------------------------------------------------
    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        if (isServer) ServerHandle(requester);
        else CmdRequestInteract(requester);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestInteract(NetworkIdentity requester, NetworkConnectionToClient sender = null)
    {
        if (requester != null && sender != null && requester.connectionToClient != sender) return;
        ServerHandle(requester);
    }

    [Server]
    private void ServerHandle(NetworkIdentity requester)
    {
        if (Tile == null || RealPhase() == ConstructionPhase.Complete) return;

        float before = RealProgress();

        switch (Tile)
        {
            case WoodTile wood: wood.ServerInteract(requester); break;
            case GlassTile glass: glass.ServerInteract(requester); break;
            case BrickTile brick: brick.ServerInteract(requester); break;
        }

        if (RealPhase() == ConstructionPhase.Complete)
        {
            progress = 1f;
            complete = true;
            RpcFinished();
            InteractionDebug.Log($"Tile_MP: '{name}' completed.");
        }
        else
        {
            float after = RealProgress();
            if (Mathf.Approximately(before, after)) return;

            progress = after;
            RpcNail();
        }
    }

    [ClientRpc]
    private void RpcNail() => VfxManager.Play(VfxId.TileNail, transform.position);

    [ClientRpc]
    private void RpcFinished() => VfxManager.Play(VfxId.TileFinished, transform.position);

    // ---- server: the synced values always follow the real tile ------------------------------------------------
    // A tile can change without passing through ServerHandle (it breaks and resets, a fallback path finishes it).
    [ServerCallback]
    private void Update()
    {
        if (Time.unscaledTime < nextServerCheckAt || Tile == null) return;
        nextServerCheckAt = Time.unscaledTime + 0.25f;

        bool isComplete = RealPhase() == ConstructionPhase.Complete;
        if (isComplete != complete) complete = isComplete;

        float value = isComplete ? 1f : RealProgress();
        if (!Mathf.Approximately(value, progress)) progress = value;
    }

    // ---- client: apply the synced values to the local tile ------------------------------------------------------
    public override void OnStartClient() => ApplyToLocalTile();

    private void OnEnable()
    {
        // The SP tile just reset itself (Construction, no progress): the server does the same here, a client re-applies.
        if (isServer)
        {
            complete = false;
            progress = 0f;
            return;
        }

        if (!isClient) return;

        ApplyToLocalTile();
        StartCoroutine(ApplyNextFrame()); // the SP tile's own OnEnable may run after this one
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;
        ApplyToLocalTile();
    }

    private void OnProgressChanged(float oldValue, float newValue) => ApplyToLocalTile();

    private void OnCompleteChanged(bool oldValue, bool newValue) => ApplyToLocalTile();

    // Only while the tile is switched on: applying to a tile that is off would be wiped by its next OnEnable, and the
    // room counter would then be reported twice.
    private void ApplyToLocalTile()
    {
        if (isServer || Tile == null || !gameObject.activeInHierarchy) return;

        if (complete)
        {
            MpSharedCompat.ApplyTileCompletedOnClient(Tile);
            return;
        }

        if (progress <= 0f) return;

        switch (Tile)
        {
            case WoodTile wood: wood.ApplyNetworkedProgress(progress); break;
            case GlassTile glass: glass.ApplyNetworkedProgress(progress); break;
            case BrickTile brick: brick.ApplyNetworkedProgress(progress); break;
        }
    }

    // ---- the real (SP) tile -------------------------------------------------------------------------------------
    private ConstructionPhase RealPhase()
    {
        switch (Tile)
        {
            case WoodTile wood: return wood.GetCurrentPhase();
            case GlassTile glass: return glass.GetCurrentPhase();
            case BrickTile brick: return brick.GetCurrentPhase();
            default: return ConstructionPhase.Construction;
        }
    }

    private float RealProgress()
    {
        switch (Tile)
        {
            case WoodTile wood: return wood.GetProcessNormalized();
            case GlassTile glass: return glass.GetProcessNormalized();
            case BrickTile brick: return brick.GetProcessNormalized();
            default: return 0f;
        }
    }
}
