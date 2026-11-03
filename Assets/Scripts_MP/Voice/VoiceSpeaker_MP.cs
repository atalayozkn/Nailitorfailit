using Steamworks;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VoiceSpeaker_MP : MonoBehaviour
{
    private const int MaxPcmBytes = 20480;
    private const int MaxBufferedSeconds = 2;

    private AudioSource source;
    private AudioClip clip;

    private float[] ring = new float[48000];
    private int readIndex;
    private int writeIndex;
    private int count;
    private readonly object gate = new object();

    private readonly byte[] pcm = new byte[MaxPcmBytes];
    private int sampleRate;

    private string speakerName;

    public float LastPacketTime { get; private set; } = -999f;

    public bool IsSpeaking => Time.unscaledTime - LastPacketTime < 0.35f;

    public string SpeakerName
    {
        get
        {
            if (!string.IsNullOrEmpty(speakerName)) return speakerName;

            LobbyPlayer lobbyPlayer = GetComponent<LobbyPlayer>();

            if (lobbyPlayer != null && !string.IsNullOrEmpty(lobbyPlayer.playerName))
                speakerName = lobbyPlayer.playerName;
            else
                speakerName = gameObject.name;

            return speakerName;
        }
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 2f;
        source.maxDistance = 25f;
        source.dopplerLevel = 0f;
        source.volume = 1f;
    }

    private void OnDestroy()
    {
        if (clip == null) return;

        if (source != null) source.Stop();

        Destroy(clip);
        clip = null;
    }

    public void Push(byte[] compressed)
    {
        if (compressed == null || compressed.Length == 0) return;
        if (!SteamManager.Initialized) return;

        EnsureClip();

        if (clip == null) return;

        EVoiceResult result = SteamUser.DecompressVoice(compressed, (uint)compressed.Length, pcm, (uint)pcm.Length,
                                                        out uint written, (uint)sampleRate);

        if (result != EVoiceResult.k_EVoiceResultOK || written == 0) return;

        LastPacketTime = Time.unscaledTime;

        int samples = (int)(written / 2);
        const float scale = 1f / 32768f;

        lock (gate)
        {
            for (int i = 0; i < samples; i++)
            {
                short value = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));

                ring[writeIndex] = value * scale;
                writeIndex = (writeIndex + 1) % ring.Length;

                if (count < ring.Length) count++;
                else readIndex = (readIndex + 1) % ring.Length;
            }
        }
    }

    private void EnsureClip()
    {
        if (clip != null) return;

        sampleRate = (int)SteamUser.GetVoiceOptimalSampleRate();

        if (sampleRate <= 0) sampleRate = 48000;

        ring = new float[sampleRate * MaxBufferedSeconds];

        clip = AudioClip.Create("voice", sampleRate, 1, sampleRate, true, OnAudioRead);

        source.clip = clip;
        source.Play();
    }

    private void OnAudioRead(float[] data)
    {
        lock (gate)
        {
            int index = 0;

            for (; index < data.Length && count > 0; index++)
            {
                data[index] = ring[readIndex];
                readIndex = (readIndex + 1) % ring.Length;
                count--;
            }

            for (; index < data.Length; index++) data[index] = 0f;
        }
    }
}
