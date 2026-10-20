using Mirror;
using UnityEngine;

/// <summary>
/// Multiplayer pause is a LOCAL menu, never a global time stop.
///
/// The SP pause flow (PauseMenu -> GamePauseManager.SetPause) sets Time.timeScale = 0,
/// which freezes every simulated object on this machine: the other players' avatars,
/// the dog, traps, timers - the whole shared world stops while one player reads the
/// menu. In MP the menu must only stop the player who opened it.
///
/// The authored SP PauseMenu is switched off by MpLevelInstaller; the owner-only
/// PauseMenu on the Mirror player prefab stays, so the panel and the cursor still
/// work exactly as before. This guard then:
///
///  - restores Time.timeScale immediately whenever a network session is running,
///  - stops (and restarts) only the LOCAL player's movement/interaction while the
///    menu is open,
///  - makes sure leaving the session while paused does not leave time frozen.
///
/// It is a self-spawning DontDestroyOnLoad singleton, like the other MP services, so
/// it needs no scene or prefab wiring.
/// </summary>
public class MpPauseGuard_MP : MonoBehaviour
{
    public static MpPauseGuard_MP Instance { get; private set; }

    private bool wasPaused;
    private bool loggedTimeScaleOverride;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpPauseGuard_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpPauseGuard_MP").AddComponent<MpPauseGuard_MP>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        bool networked = NetworkServer.active || NetworkClient.active;

        if (!networked)
        {
            // Leaving (or never joining) a session while the local menu was open must
            // not leave the next scene frozen.
            if (wasPaused)
            {
                wasPaused = false;
                if (Time.timeScale <= 0f) Time.timeScale = 1f;
                ApplyLocalControls(true);
            }

            return;
        }

        if (Time.timeScale <= 0f)
        {
            Time.timeScale = 1f;

            if (!loggedTimeScaleOverride)
            {
                loggedTimeScaleOverride = true;
                Debug.Log("[MP] Pause is local: the world keeps running while the pause menu is open.");
            }
        }

        GamePauseManager pause = GamePauseManager.Instance;
        bool paused = pause != null && pause.IsPaused;

        if (paused == wasPaused) return;

        wasPaused = paused;
        ApplyLocalControls(!paused);
    }

    /// <summary>
    /// GamePauseManager caches the movement/interaction handlers of the AUTHORED SP
    /// avatar (which MP disables), so it no longer reaches the networked local player.
    /// Apply the pause to the local Mirror player explicitly.
    /// </summary>
    private static void ApplyLocalControls(bool active)
    {
        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) return;

        PlayerMovement movement = localPlayer.GetComponent<PlayerMovement>();
        if (movement != null) movement.SetActivity(active);

        PlayerInteractionHandler interaction = localPlayer.GetComponent<PlayerInteractionHandler>();
        if (interaction != null) interaction.SetActivity(active);
    }
}
