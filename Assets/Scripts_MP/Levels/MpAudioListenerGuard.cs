using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps exactly ONE enabled AudioListener alive.
///
/// The MP player prefab has its listener inside "LocalPlayerOnly" (so each machine has
/// exactly one for its own player), but level scenes bring their own camera listener -
/// which produces the endless
///     "There are 2 audio listeners in the scene. Please ensure there is always exactly
///      one audio listener in the scene."
/// warning in every MP level. This guard prefers the local player's listener and disables
/// the others, and re-checks whenever a scene loads.
/// </summary>
public class MpAudioListenerGuard : MonoBehaviour
{
    public static MpAudioListenerGuard Instance { get; private set; }

    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private bool logChanges = true;

    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpAudioListenerGuard>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpAudioListenerGuard").AddComponent<MpAudioListenerGuard>();
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
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + Mathf.Max(0.25f, checkInterval);

        Enforce();
    }

    /// <summary>Enables the local player's listener, disables every other one.</summary>
    public void Enforce()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (listeners.Length <= 1) return;

        // prefer the listener that belongs to the local player
        AudioListener preferred = null;

        if (NetworkClient.active && NetworkClient.localPlayer != null)
            preferred = NetworkClient.localPlayer.GetComponentInChildren<AudioListener>(true);

        if (preferred == null)
            preferred = listeners[0];

        bool changed = false;

        foreach (AudioListener listener in listeners)
        {
            if (listener == null) continue;

            bool shouldBeOn = listener == preferred;

            // AudioListener uses 'enabled' (it is a Behaviour), so toggle that
            if (listener.enabled != shouldBeOn)
            {
                listener.enabled = shouldBeOn;
                changed = true;
            }
        }

        if (changed && logChanges)
            Debug.Log($"[MP-AUDIO] kept one listener ('{preferred.gameObject.name}'), disabled the others " +
                      $"({listeners.Length} found).");
    }
}
