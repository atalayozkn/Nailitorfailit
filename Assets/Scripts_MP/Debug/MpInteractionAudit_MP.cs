using System.Collections.Generic;
using System.Text;
using Interactions;
using Interactions.Networking;
using ItemScript;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MpInteractionAudit_MP : MonoBehaviour
{
    [Tooltip("Sahne yuklendiginde etkilesim kapsama raporu bas.")]
    [SerializeField] private bool logReport = true;

    private static readonly HashSet<string> Reported = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpInteractionAudit_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpInteractionAudit_MP").AddComponent<MpInteractionAudit_MP>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        Audit(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Audit(scene);
    }

    private void Audit(Scene scene)
    {
        if (!logReport) return;
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (!NetworkServer.active && !NetworkClient.active) return;
        if (!Reported.Add(scene.path)) return;

        int total = 0;
        int covered = 0;
        var uncovered = new List<string>();
        var withoutIdentity = new List<string>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null) continue;

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                if (!(behaviour is IInteractable)) continue;

                total++;

                bool hasProxy = behaviour.GetComponent<IInteractionNetworkProxy>() != null ||
                                behaviour.GetComponentInParent<IInteractionNetworkProxy>(true) != null;

                bool routedByPlayer = behaviour is CarriableObject_SP ||
                                      behaviour is FoodBowl ||
                                      behaviour is MailBox ||
                                      behaviour is DogInteractionHelper ||
                                      behaviour is PlayerInteractionHandler;

                bool tileViaConstructor = (behaviour is WoodTile || behaviour is GlassTile || behaviour is BrickTile) &&
                                          behaviour.GetComponentInParent<Constructor_MP>(true) != null;

                bool hasIdentity = behaviour.GetComponentInParent<NetworkIdentity>(true) != null;

                if (hasProxy || routedByPlayer || tileViaConstructor)
                {
                    covered++;

                    if (!hasIdentity && !(behaviour is PlayerInteractionHandler) && !routedByPlayer)
                        withoutIdentity.Add($"{behaviour.GetType().Name} '{behaviour.name}' @ {PathOf(behaviour.transform)}");

                    continue;
                }

                uncovered.Add($"{behaviour.GetType().Name} '{behaviour.name}' " +
                              $"(identity={(hasIdentity ? "var" : "YOK")}) @ {PathOf(behaviour.transform)}");
            }
        }

        var sb = new StringBuilder();
        sb.Append($"[MP-AUDIT] '{scene.name}': {total} interactable, {covered} kapsandi, {uncovered.Count} KAPSANMADI");

        if (uncovered.Count > 0)
        {
            sb.Append('\n');
            int limit = Mathf.Min(uncovered.Count, 25);
            for (int i = 0; i < limit; i++) sb.Append("  - ").Append(uncovered[i]).Append('\n');
        }

        if (withoutIdentity.Count > 0)
        {
            sb.Append($"  ({withoutIdentity.Count} proxy'li ama NetworkIdentity'siz)\n");
            int limit = Mathf.Min(withoutIdentity.Count, 10);
            for (int i = 0; i < limit; i++) sb.Append("  ~ ").Append(withoutIdentity[i]).Append('\n');
        }

        if (uncovered.Count == 0 && withoutIdentity.Count == 0)
            sb.Append(" - sorun yok.");

        Debug.Log(sb.ToString());
    }

    private static string PathOf(Transform transform)
    {
        var sb = new StringBuilder(transform.name);

        for (Transform node = transform.parent; node != null; node = node.parent)
            sb.Insert(0, node.name + "/");

        return sb.ToString();
    }
}
