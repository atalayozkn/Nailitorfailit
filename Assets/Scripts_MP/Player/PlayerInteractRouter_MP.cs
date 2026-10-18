using System.Text;
using Interactions;
using Interactions.Networking;
using ItemScript;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Server-side router for interactables that have NO NetworkIdentity of their own.
///
/// Why this exists: several interactables live WITHOUT an identity of their own -
/// the in-level shop shelves (prefab variants) and the per-player dog bowl inside
/// the player prefab. Putting an identity on every prop does not work (variants), so
/// the interaction travels on the PLAYER's identity instead and the object is
/// addressed by scene + hierarchy path - a key every machine resolves identically.
///
/// Server-side handling:
///   1. an IInteractionNetworkProxy on the object (if it has one),
///   2. the dog bowl (per-player, contents replicated with a SyncVar),
///   3. the shop (Shop.ServerInteract - networked spawn + hand-off + wallet),
///   4. otherwise it is logged and skipped (never silently run the SP path, which
///      would only mutate the host's copy).
/// </summary>
public class PlayerInteractRouter_MP : NetworkBehaviour
{
    [Tooltip("Per-player dog bowl. Filled on the host, replicated to everyone.")]
    [SyncVar(hook = nameof(OnBowlFoodChanged))]
    private int bowlFood = -1;   // -1 = not initialized yet

    private FoodBowl cachedBowl;

    // ---------------------------------------------------------------- local entry

    public void RequestInteract(Component interactable)
    {
        if (interactable == null) return;

        string sceneName = interactable.gameObject.scene.name;
        string path = PathOf(interactable.transform);

        InteractionDebug.Log($"[{name}] player-routed interact: '{interactable.name}' " +
                             $"({sceneName} :: {path})");

        if (isServer) ServerRoute(sceneName, path, netIdentity);
        else CmdRequestInteract(sceneName, path);
    }

    /// <summary>Local entry for the dog bowl (called by FoodBowl.OnInteract).</summary>
    public void RequestFeedBowl(FoodBowl bowl)
    {
        InteractionDebug.Log($"[{name}] feed-bowl request (local entry), bowl='{bowl?.name}'");

        if (isServer) ServerFeedBowl(netIdentity);
        else CmdRequestFeedBowl();
    }

    [Command]
    private void CmdRequestInteract(string sceneName, string path) => ServerRoute(sceneName, path, netIdentity);

    [Command]
    private void CmdRequestFeedBowl() => ServerFeedBowl(netIdentity);

    // ---------------------------------------------------------------- server side

    [Server]
    private void ServerRoute(string sceneName, string path, NetworkIdentity requester)
    {
        if (string.IsNullOrEmpty(path))
        {
            InteractionDebug.Log("player-routed interact: empty path, ignored.");
            return;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid())
        {
            InteractionDebug.Log($"player-routed interact: scene '{sceneName}' is not loaded, ignored.");
            return;
        }

        Transform target = FindByPath(scene, path);

        if (target == null) target = FindByPathTail(scene, path);

        if (target == null)
        {
            InteractionDebug.Log($"player-routed interact: '{path}' not found in '{sceneName}' " +
                                 $"(also searched by path tail), ignored.");
            return;
        }

        // 1) object owns a proxy -> the normal path
        IInteractionNetworkProxy proxy = target.GetComponent<IInteractionNetworkProxy>();
        if (proxy != null)
        {
            Component interactable = FindInteractable(target);
            proxy.RequestInteract(requester, interactable);
            return;
        }

        // 2) per-player dog bowl: the requester's OWN bowl is the one to fill
        if (target.TryGetComponent(out FoodBowl _))
        {
            ServerFeedBowl(requester);
            return;
        }

        // 3) known MP-aware interactables
        if (target.TryGetComponent(out Shop shop))
        {
            shop.ServerInteract(requester);
            return;
        }

        // 4) nothing MP-aware here
        InteractionDebug.Log($"player-routed interact: '{path}' has no MP handler " +
                             $"(add a proxy or a ServerInteract method).");
    }

    /// <summary>
    /// Feeds the requester's dog bowl: consumes the carried PetFood through the
    /// networked path and raises the replicated bowl contents.
    /// </summary>
    [Server]
    public void ServerFeedBowl(NetworkIdentity requester)
    {
        FoodBowl bowl = ResolveBowlFor(requester);
        if (bowl == null)
        {
            Debug.LogError($"PlayerInteractRouter_MP: no FoodBowl found for " +
                           $"'{requester?.name ?? "host"}'.", this);
            return;
        }

        PlayerInteractionHandler handler = PlayerInteractionResolver.ForIdentity(requester);
        CarriableObject_SP carried = handler != null ? handler.GetCurrentCarriable() : null;

        InteractionDebug.Log($"ServerFeedBowl: bowl='{bowl.name}' handler={(handler != null)} " +
                             $"carried='{(carried != null ? carried.name : "-")}' " +
                             $"type={(carried != null ? carried.carriableType.ToString() : "-")} " +
                             $"food={bowlFood}/{bowl.GetMaxFood()}");

        if (carried == null || carried.carriableType != CarriableType.PetFood)
        {
            InteractionDebug.Log("ServerFeedBowl: refused - the requester is not carrying pet food.");
            return;
        }

        // consume the food for everyone
        if (carried.TryGetComponent(out CarriableObject_MP carriableMp))
            carriableMp.ServerConsumeNetworked(requester);
        else
            carried.OnConsume();

        if (handler != null) handler.ClearCarriedObject();

        int current = bowlFood < 0 ? bowl.GetCurrentFood() : bowlFood;
        bowlFood = Mathf.Clamp(current + bowl.GetPerFillAmount(), 0, bowl.GetMaxFood());

        InteractionDebug.Log($"ServerFeedBowl: bowl now {bowlFood}/{bowl.GetMaxFood()}");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // The bowl starts full (FoodBowl.Awake), and a SyncVar's serialized default is
        // 0 - so the host seeds the replicated value from the bowl itself.
        FoodBowl bowl = ResolveBowlFor(netIdentity);
        if (bowl != null) bowlFood = bowl.GetCurrentFood();
    }

    private void OnBowlFoodChanged(int oldValue, int newValue)
    {
        if (newValue < 0) return;

        FoodBowl bowl = cachedBowl != null ? cachedBowl : (cachedBowl = GetComponentInChildren<FoodBowl>(true));
        if (bowl != null) bowl.ApplyNetworkedFood(newValue);
    }

    private FoodBowl ResolveBowlFor(NetworkIdentity requester)
    {
        // The bowl belongs to the requester's own player object.
        if (requester != null)
        {
            FoodBowl owned = requester.GetComponentInChildren<FoodBowl>(true);
            if (owned != null) return owned;
        }

        return GetComponentInChildren<FoodBowl>(true);
    }

    // ---------------------------------------------------------------- path helpers

    private static Transform FindByPath(Scene scene, string path)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = root.transform.Find(path);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Second chance: any object in that scene whose full hierarchy path ends with the
    /// requested path (handles inactive roots and renamed prefab instances).
    /// </summary>
    private static Transform FindByPathTail(Scene scene, string path)
    {
        foreach (Shop shop in UnityEngine.Object.FindObjectsByType<Shop>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (shop == null) continue;
            if (shop.gameObject.scene != scene) continue;

            string candidate = PathOf(shop.transform);

            if (candidate == path || candidate.EndsWith("/" + path) || path.EndsWith("/" + candidate))
            {
                InteractionDebug.Log($"player-routed interact: matched '{shop.name}' by path tail " +
                                     $"('{candidate}').");
                return shop.transform;
            }
        }

        // bowls are inside player instances (which are clones and therefore never
        // match a baked path)
        foreach (FoodBowl bowl in UnityEngine.Object.FindObjectsByType<FoodBowl>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bowl == null) continue;
            if (bowl.gameObject.scene != scene) continue;

            InteractionDebug.Log($"player-routed interact: matched bowl '{bowl.name}' on " +
                                 $"'{bowl.transform.root.name}'.");
            return bowl.transform;
        }

        return null;
    }

    private static Component FindInteractable(Transform target)
    {
        foreach (Component c in target.GetComponents<Component>())
        {
            if (c is IInteractable) return c;
        }

        return target;
    }

    /// <summary>Hierarchy path of a transform below its scene root ("A/B/C").</summary>
    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);

        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }

        return sb.ToString();
    }
}
