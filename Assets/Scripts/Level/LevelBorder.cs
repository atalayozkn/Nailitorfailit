using System.Collections;
using Interactions;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// The player cannot walk out of the level: an invisible rectangle around everything the player works with (every
// interactable, the spawn points) plus a margin. Singleplayer and multiplayer, Level 1-5 and the Compete level.
// It only moves the player (the one controlled on this machine) back: cars and other objects are not touched.
// Done by position, not by wall colliders, so roads that leave the map keep working.
public class LevelBorder : MonoBehaviour
{
    private const float Margin = 7f;

    private const float FallDeathHeight = -12f;

    private static LevelBorder instance;

    private Bounds area;
    private bool ready;
    private Coroutine building;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;

        var go = new GameObject("LevelBorder");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LevelBorder>();
        instance.OnLoaded(SceneManager.GetActiveScene());
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) OnLoaded(scene);
    }

    private static bool IsLevel(Scene scene)
    {
        string n = scene.name;
        return (n.StartsWith("Level_0") && !n.Contains("TARIK") && !n.Contains("Arda")) || n.StartsWith("VersusMultiplayer");
    }

    private void OnLoaded(Scene scene)
    {
        ready = false;
        if (building != null) StopCoroutine(building);
        if (IsLevel(scene)) building = StartCoroutine(Build(scene));
    }

    // Built a moment after the scene starts (the multiplayer installer and the spawned objects are there by then) and
    // once more a little later, so late objects count too.
    private IEnumerator Build(Scene scene)
    {
        yield return new WaitForSeconds(1.5f);
        Compute(scene);
        yield return new WaitForSeconds(4f);
        Compute(scene);
    }

    private void Compute(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        bool any = false;
        var bounds = new Bounds();

        void Add(Vector3 point)
        {
            if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
            else bounds.Encapsulate(point);
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IInteractable) Add(behaviour.transform.position);
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "SpawnPoint") Add(t.position);
            }
        }

        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Add(player.transform.position);

        if (!any) return;

        bounds.Expand(new Vector3(Margin * 2f, 0f, Margin * 2f));
        area = bounds;
        ready = true;
    }

    private static void KillFalling(PlayerMovement player)
    {
        PlayerStateMachine state = player.GetComponentInParent<PlayerStateMachine>();
        if (state == null || state.currentPlayerState == PlayerStates.Dead) return;

        state.SetDeathReason(DeathReason.None);
        state.ChangeToDeadState();
    }

    private void FixedUpdate()
    {
        if (!ready) return;

        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player == null || !player.TryGetComponent(out Rigidbody body)) continue;

            // Multiplayer: only the player this machine controls.
            NetworkIdentity identity = player.GetComponentInParent<NetworkIdentity>();
            if (identity != null && NetworkClient.active && !identity.isOwned) continue;

            Vector3 position = body.position;

            // Singleplayer: a player who falls out of the world dies (multiplayer: MpDeathZone_MP does the same).
            if (identity == null && position.y < FallDeathHeight) KillFalling(player);

            float x = Mathf.Clamp(position.x, area.min.x, area.max.x);
            float z = Mathf.Clamp(position.z, area.min.z, area.max.z);
            if (Mathf.Approximately(x, position.x) && Mathf.Approximately(z, position.z)) continue;

            body.position = new Vector3(x, position.y, z);

            Vector3 velocity = body.linearVelocity;
            if (!Mathf.Approximately(x, position.x)) velocity.x = 0f;
            if (!Mathf.Approximately(z, position.z)) velocity.z = 0f;
            body.linearVelocity = velocity;
        }
    }
}
