using Mirror;
using UnityEngine;

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
