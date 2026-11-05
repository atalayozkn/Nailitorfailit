using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Plays short gameplay sounds from a small pool of AudioSources routed to the Sfx mixer group.
// Singleplayer only, like VfxManager: multiplayer keeps its own audio.
public static class Sfx
{
    private const string MixerPath = "Audio/GameAudioMixer";
    private const int PoolSize = 16;

    private static readonly List<AudioSource> pool = new();
    private static readonly Dictionary<Object, float> lastPlayed = new();
    private static AudioMixerGroup sfxGroup;
    private static int next;
    private static bool mixerLoaded;

    public static AudioMixerGroup Group
    {
        get
        {
            if (!mixerLoaded)
            {
                mixerLoaded = true;
                var mixer = Resources.Load<AudioMixer>(MixerPath);
                var groups = mixer != null ? mixer.FindMatchingGroups("Sfx") : null;
                sfxGroup = groups != null && groups.Length > 0 ? groups[0] : null;
            }
            return sfxGroup;
        }
    }

    // spatial: 0 = flat 2D (UI, stingers), 1 = fully positional.
    public static void Play(AudioClip[] clips, Vector3 position, float volume = 1f, float pitchJitter = 0.05f, float spatial = 0.6f, float minInterval = 0.04f)
    {
        if (NetworkModeHelper.IsNetworked || clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        // Many tiles can fire the same event in one frame; one sound is enough.
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clips[0], out float last) && now - last < minInterval) return;
        lastPlayed[clips[0]] = now;

        var source = NextSource();
        source.transform.position = position;
        source.spatialBlend = spatial;
        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, volume);
    }

    public static void Play2D(AudioClip[] clips, float volume = 1f, float pitchJitter = 0.03f) =>
        Play(clips, Vector3.zero, volume, pitchJitter, 0f);

    // A looping source on the host; it stops when the host is destroyed.
    public static AudioSource AttachLoop(GameObject host, AudioClip clip, float volume)
    {
        if (NetworkModeHelper.IsNetworked || host == null || clip == null) return null;
        var source = host.AddComponent<AudioSource>();
        Setup(source, 0.85f);
        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.time = Random.Range(0f, clip.length);
        source.Play();
        return source;
    }

    public static void Setup(AudioSource source, float spatial)
    {
        source.playOnAwake = false;
        source.outputAudioMixerGroup = Group;
        source.spatialBlend = spatial;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 6f;
        source.maxDistance = 40f;
        source.dopplerLevel = 0f;
    }

    private static AudioSource NextSource()
    {
        pool.RemoveAll(s => s == null);
        if (pool.Count < PoolSize)
        {
            var go = new GameObject("Sfx Source");
            Object.DontDestroyOnLoad(go);
            var source = go.AddComponent<AudioSource>();
            Setup(source, 0.6f);
            pool.Add(source);
            return source;
        }
        next = (next + 1) % pool.Count;
        return pool[next];
    }
}
