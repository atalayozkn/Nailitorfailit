using System.Collections.Generic;
using Interactions.Networking;
using ItemScript;
using Mirror;
using UnityEngine;

// Server runs the SP logic (through SharedMpCompat) and mirrors the result to all machines:
//   placing a tile into the Constructor -> RpcApplyBuilt (blueprint off, model on, collider off, VFX)
// The tiles inside have their own networked state (Tile_MP: progress + finished), so this class only knows
// "built or not". Health hits are still addressed by their child-index path under this object.
public class Constructor_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private Constructor constructor;

    // The truth about this constructor, kept in synced data and not only in a one-shot RPC: clients re-apply it every
    // second, so a lost or early RPC can't leave them without the built tile.
    //   builtType: -1 = nothing placed yet, else the CarriableType that was placed.
    [SyncVar] private int builtType = -1;
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

    // Server: remember a build that did not pass through ServerHandle, so late clients still get it.
    private float nextServerPollAt;

    private void ServerPollBuilt()
    {
        if (constructor == null || Time.unscaledTime < nextServerPollAt) return;
        nextServerPollAt = Time.unscaledTime + 0.5f;

        if (builtType < 0 && constructor.IsBuilt()) builtType = (int)constructor.GetAcceptedType();
    }

    private void Update()
    {
        if (isServer) ServerPollBuilt();

        if (isServer || !isClient || constructor == null) return;
        if (Time.unscaledTime < nextTileSyncAt) return;
        nextTileSyncAt = Time.unscaledTime + 1f;

        // Placed on the host, not here yet.
        if (builtType >= 0) MpSharedCompat.ApplyConstructorBuiltOnClient(constructor, (CarriableType)builtType);
    }

    // Tiles have their own proxy (Tile_MP); what reaches this one is the constructor itself: placing the tile.
    public void RequestInteract(NetworkIdentity requester, Component interactable) => RequestBuild(requester);

    public void RequestBuild() => RequestBuild(null);

    public void RequestBuild(NetworkIdentity requester)
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
        InteractionDebug.Log($"Constructor_MP: requester={requester?.name ?? "host"}");

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

    // "index:name/index:name": the index finds the child fast, the name guards against hierarchies that differ between
    // machines (objects added at runtime on one side only shifted the indices and addressed the wrong tile).
    private string PathTo(Transform target)
    {
        var parts = new List<string>();

        for (Transform t = target; t != null && t != transform; t = t.parent) parts.Add(t.GetSiblingIndex() + ":" + t.name);

        parts.Reverse();
        return string.Join("/", parts);
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
}
