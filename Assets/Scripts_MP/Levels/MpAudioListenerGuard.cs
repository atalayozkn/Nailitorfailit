using Mirror;
using UnityEngine;
using UnityEngine.AI;

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
        if (!NetworkServer.active && !NetworkClient.active) return;
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + Mathf.Max(0.25f, checkInterval);

        Enforce();
    }

    public void Enforce()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (listeners.Length <= 1) return;

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
