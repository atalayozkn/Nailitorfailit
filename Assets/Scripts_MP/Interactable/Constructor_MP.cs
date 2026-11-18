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

    // The truth about this constructor, kept in synced data and not only in one-shot RPCs: a lost or early RPC (tile not
    // found yet, message before the scene was ready) used to leave a client with a tile that still showed its progress
    // bar while the host had finished it. Clients re-apply this every second, so they always converge.
    //   builtType: -1 = nothing placed yet, else the CarriableType that was placed.
    //   tileProgress: tile path -> progress 0..1 (1 = finished).
    [SyncVar] private int builtType = -1;
    private readonly SyncDictionary<string, float> tileProgress = new SyncDictionary<string, float>();
    private float nextTileSyncAt;

    private void Awake()
    {
        constructor = GetComponent<Constructor>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        nextTileSyncAt = 0f;
    }

    // ---- tile health: decided on the server, applied identically everywhere ----
    // A tile's health used to be local to each machine: a client that walked over it with a heavy item, or stood in a
    // fire, broke the tile ON ITS OWN MACHINE only (it reset to its blueprint and showed the progress bar again) while
    // the host still had it built. Now every hit goes to the server, which applies it and sends the same hit to the
    // other machines, so all copies lose health in step and die together.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDamageRouter()
    {
        ObjectHealth.NetworkDamageRouter = RouteDamage;
    }

    private static readonly Dictionary<string, float> lastDamageAt = new Dictionary<string, float>();
    private const float DamageDedupeSeconds = 0.25f;

    private static bool RouteDamage(ObjectHealth health)
    {
        if (health == null) return false;

        Constructor_MP owner = health.GetComponentInParent<Constructor_MP>();
        if (owner == null || owner.netIdentity == null) return false; // not part of a networked constructor: local, as before

        string path = owner.PathTo(health.transform);
        if (path.Length == 0) path = "."; // the health component sits on the constructor itself

        if (owner.isServer) owner.ServerDamage(path);
        else if (NetworkClient.active) owner.CmdDamage(path);

        return true;
    }

    [Command(requiresAuthority = false)]
    private void CmdDamage(string path) => ServerDamage(path);

    [Server]
    private void ServerDamage(string path)
    {
        // Every machine that sees the same fire or the same pressure asks: one hit counts once.
        string key = netId + "|" + path;
        if (lastDamageAt.TryGetValue(key, out float last) && Time.unscaledTime - last < DamageDedupeSeconds) return;
        lastDamageAt[key] = Time.unscaledTime;

        ObjectHealth health = FindHealth(path);
        if (health == null) return;

        health.DealDamageLocal();
        RpcDamage(path);
    }

    [ClientRpc]
    private void RpcDamage(string path)
    {
        if (isServer) return;

        ObjectHealth health = FindHealth(path);
        if (health != null) health.DealDamageLocal();
    }

    private ObjectHealth FindHealth(string path)
    {
        Transform target = FindByPath(path);
        return target != null ? target.GetComponent<ObjectHealth>() : null;
    }

    private void Update()
    {
        if (isServer || !isClient || constructor == null) return;
        if (Time.unscaledTime < nextTileSyncAt) return;
        nextTileSyncAt = Time.unscaledTime + 1f;

        // Placed on the host, not here yet.
        if (builtType >= 0) MpSharedCompat.ApplyConstructorBuiltOnClient(constructor, (CarriableType)builtType);

        foreach (KeyValuePair<string, float> entry in tileProgress)
        {
            Component tile = FindTile(entry.Key);
            if (tile == null) continue;

            if (entry.Value >= 1f) MpSharedCompat.ApplyTileCompletedOnClient((MonoBehaviour)tile);
            else ApplyTileProgress(tile, entry.Value);
        }
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

            tileProgress[tilePath] = completed ? 1f : after;

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

        if (constructor.IsBuilt())
        {
            builtType = (int)constructor.GetAcceptedType();
            RpcApplyBuilt(builtType);
        }
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

    // "index:name/index:name": the index finds the child fast, the name guards against hierarchies that differ between
    // machines (objects added at runtime on one side only shifted the indices and addressed the wrong tile).
    private string PathTo(Transform target)
    {
        var parts = new List<string>();

        for (Transform t = target; t != null && t != transform; t = t.parent) parts.Add(t.GetSiblingIndex() + ":" + t.name);

        parts.Reverse();
        return string.Join("/", parts);
    }

    private Component FindTile(string tilePath)
    {
        Transform current = FindByPath(tilePath);
        if (current == null) return null;

        foreach (Component candidate in current.GetComponents<Component>())
        {
            if (IsTile(candidate)) return candidate;
        }

        return null;
    }

    private Transform FindByPath(string tilePath)
    {
        if (string.IsNullOrEmpty(tilePath)) return null;
        if (tilePath == ".") return transform;

        Transform current = transform;

        foreach (string part in tilePath.Split('/'))
        {
            int colon = part.IndexOf(':');
            string indexText = colon >= 0 ? part.Substring(0, colon) : part;
            string childName = colon >= 0 ? part.Substring(colon + 1) : null;

            if (!int.TryParse(indexText, out int index)) return null;

            Transform next = null;
            if (index >= 0 && index < current.childCount)
            {
                Transform byIndex = current.GetChild(index);
                if (childName == null || byIndex.name == childName) next = byIndex;
            }

            if (next == null && childName != null)
            {
                for (int i = 0; i < current.childCount; i++)
                {
                    if (current.GetChild(i).name != childName) continue;
                    next = current.GetChild(i);
                    break;
                }
            }

            if (next == null) return null;
            current = next;
        }

        return current;
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
