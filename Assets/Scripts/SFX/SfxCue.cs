using UnityEngine;

// Sound for a prefab's UnityEvent: hook Play, or PlayLoop/StopLoop, from the event in the Inspector.
public class SfxCue : MonoBehaviour
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField, Range(0f, 0.3f)] private float pitchJitter = 0.06f;
    [SerializeField, Range(0f, 1f)] private float spatial = 0.6f;
    [Tooltip("Ignores repeats faster than this, for events that can fire every frame.")]
    [SerializeField] private float minInterval = 0.05f;

    private AudioSource loopSource;

    public void Setup(AudioClip[] sounds, float vol, float jitter, float spatialBlend, float interval)
    {
        clips = sounds;
        volume = vol;
        pitchJitter = jitter;
        spatial = spatialBlend;
        minInterval = interval;
    }

    public void Play() => Sfx.Play(clips, transform.position, volume, pitchJitter, spatial, minInterval);

    public void PlayLoop()
    {
        if (loopSource != null || clips == null || clips.Length == 0) return;
        loopSource = Sfx.AttachLoop(gameObject, clips[0], volume);
    }

    public void StopLoop()
    {
        if (loopSource == null) return;
        Destroy(loopSource);
        loopSource = null;
    }
}
