using Steamworks;
using UnityEngine;

// Plays one remote player's Steam voice. Lives on the player's NetworkIdentity object (the voice UI looks it
// up there), but the AudioSource sits on a child that follows the moving avatar: the package root stays at
// the spawn point, so a 3D source on it went silent as soon as the player walked away from spawn.
// In the lobby (no avatar) the voice is 2D. A short jitter buffer avoids the crackle of an empty ring.
public class VoiceSpeaker_MP : MonoBehaviour
{
    private const int MaxPcmBytes = 20480;
    private const int MaxBufferedSeconds = 2;
    private const float PrimeSeconds = 0.06f;

    private AudioSource source;
    private Transform sourceTransform;
    private Transform avatar;
    private float nextAvatarLookup;

    private AudioClip clip;
    private float[] ring = new float[48000];
    private int readIndex;
    private int writeIndex;
    private int count;
    private bool primed;
    private int primeSamples = 2880;
    private readonly object gate = new object();
    private readonly byte[] pcm = new byte[MaxPcmBytes];
    private int sampleRate;

    private string speakerName;
    private float nextDecodeWarnAt;

    public float LastPacketTime { get; private set; } = -999f;
    public bool IsSpeaking => Time.unscaledTime - LastPacketTime < 0.35f;

    public string SpeakerName
    {
        get
        {
            if (!string.IsNullOrEmpty(speakerName)) return speakerName;

            LobbyPlayer lobbyPlayer = GetComponent<LobbyPlayer>();
            if (lobbyPlayer != null && !string.IsNullOrEmpty(lobbyPlayer.playerName)) return lobbyPlayer.playerName;

            return gameObject.name;
        }
    }

    public void SetSpeakerName(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) speakerName = value.Trim();
    }

    private void Awake()
    {
        var child = new GameObject("VoiceSource");
        sourceTransform = child.transform;
        sourceTransform.SetParent(transform, false);

        source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 8f;
        source.maxDistance = 60f;
        source.dopplerLevel = 0f;
        source.volume = 1f;
        source.spatialBlend = 0f;
    }

    private void LateUpdate()
    {
        if (avatar == null && Time.unscaledTime >= nextAvatarLookup)
        {
            nextAvatarLookup = Time.unscaledTime + 1f;

            PlayerScripts.PlayerNetwork_MP body = GetComponentInChildren<PlayerScripts.PlayerNetwork_MP>(true);
            avatar = body != null ? body.transform : null;

            // 3D only when there is a body to hear it from; lobby voice stays 2D.
            source.spatialBlend = avatar != null ? 1f : 0f;
        }

        if (avatar != null) sourceTransform.position = avatar.position + Vector3.up * 1.6f;
    }

    private void OnDestroy()
    {
        if (source != null) source.Stop();
        if (clip != null) Destroy(clip);
        clip = null;
    }

    private const int RawRate = 16000;

    // Steam voice comes out quiet (it is level-matched for a headset on top of the game audio) and the 3D roll-off
    // makes it quieter still: a fixed boost, hard-limited so loud speech cannot distort.
    private const float PlaybackGain = 3f;

    public void Push(byte[] compressed, bool raw = false)
    {
        if (compressed == null || compressed.Length == 0) return;

        // Raw PCM from a peer without Steam (local tests): no codec, plays at 16 kHz.
        if (raw)
        {
            EnsureClip(RawRate);
            if (clip == null || sampleRate != RawRate) return;

            LastPacketTime = Time.unscaledTime;
            AppendPcm(compressed, compressed.Length);
            return;
        }

        if (!SteamManager.Initialized) return;

        EnsureClip(0);
        if (clip == null) return;

        EVoiceResult result = SteamUser.DecompressVoice(compressed, (uint)compressed.Length, pcm, (uint)pcm.Length,
                                                        out uint written, (uint)sampleRate);

        if (result != EVoiceResult.k_EVoiceResultOK || written == 0)
        {
            if (Time.unscaledTime >= nextDecodeWarnAt)
            {
                nextDecodeWarnAt = Time.unscaledTime + 5f;
                Debug.LogWarning($"[VOICE] decompress failed for '{SpeakerName}': {result} (bytes={compressed.Length}, rate={sampleRate})");
            }
            return;
        }

        LastPacketTime = Time.unscaledTime;
        AppendPcm(pcm, (int)written);
    }

    private void AppendPcm(byte[] bytes, int length)
    {
        int samples = length / 2;
        const float scale = 1f / 32768f;

        lock (gate)
        {
            for (int i = 0; i < samples; i++)
            {
                short value = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                ring[writeIndex] = Mathf.Clamp(value * scale * PlaybackGain, -1f, 1f);
                writeIndex = (writeIndex + 1) % ring.Length;

                if (count < ring.Length) count++;
                else readIndex = (readIndex + 1) % ring.Length;
            }
        }
    }

    private void EnsureClip(int forcedRate)
    {
        if (clip != null) return;

        sampleRate = forcedRate > 0 ? forcedRate : (int)SteamUser.GetVoiceOptimalSampleRate();
        if (sampleRate <= 0) sampleRate = 48000;

        ring = new float[sampleRate * MaxBufferedSeconds];
        primeSamples = Mathf.CeilToInt(sampleRate * PrimeSeconds);

        clip = AudioClip.Create("voice", sampleRate, 1, sampleRate, true, OnAudioRead);
        source.clip = clip;
        source.Play();
    }

    // Audio thread.
    private void OnAudioRead(float[] data)
    {
        lock (gate)
        {
            if (!primed && count >= primeSamples) primed = true;

            int index = 0;

            if (primed)
            {
                for (; index < data.Length && count > 0; index++)
                {
                    data[index] = ring[readIndex];
                    readIndex = (readIndex + 1) % ring.Length;
                    count--;
                }

                if (count == 0) primed = false; // ran dry: wait for a fresh cushion before playing again
            }

            for (; index < data.Length; index++) data[index] = 0f;
        }
    }
}
