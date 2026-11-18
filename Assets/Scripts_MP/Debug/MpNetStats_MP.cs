using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

// On-screen network and performance tools for multiplayer tests.
//   Always (while connected): your ping in ms, top right.
//   F1 (with the console): network graph   - ping (RTT) history, jitter, KB/s in and out, messages per second, per-player ping table.
//   F1 (with the console): analysis graph  - frame time history with spikes highlighted, FPS, 1 % low, GC activity, list of recent spikes.
// Drawn with IMGUI so it works in builds without any scene setup.
public class MpNetStats_MP : MonoBehaviour
{
    private const int NetSamples = 240;       // 240 x 0.25 s = last minute
    private const float NetInterval = 0.25f;
    private const int FrameSamples = 300;     // last 300 frames
    private const float SpikeMs = 50f;        // a frame this slow is listed as a spike
    private const int SpikeLog = 6;

    private static MpNetStats_MP instance;

    private bool showNet;
    private bool showPerf;

    // network history
    private readonly float[] rtt = new float[NetSamples];
    private readonly float[] jitter = new float[NetSamples];
    private readonly float[] kbIn = new float[NetSamples];
    private readonly float[] kbOut = new float[NetSamples];
    private int netHead;
    private int netCount;
    private float nextNetSampleAt;
    private long bytesIn, bytesOut;
    private long msgsIn, msgsOut;
    private float lastMsgsIn, lastMsgsOut;
    private float rttMax;

    // frame history
    private readonly float[] frameMs = new float[FrameSamples];
    private int frameHead;
    private int frameCount;
    private readonly List<string> spikes = new List<string>();
    private int gcCollections;
    private int lastGcCount;
    private float gcWindowStart;
    private int gcPerSecond;
    private float worstMs;
    private float worstResetAt;

    private GUIStyle label;
    private GUIStyle title;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (instance != null || FindAnyObjectByType<MpNetStats_MP>(FindObjectsInactive.Include) != null) return;

        var host = new GameObject("MpNetStats_MP");
        DontDestroyOnLoad(host);
        host.AddComponent<MpNetStats_MP>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        lastGcCount = GC.CollectionCount(0);
        gcWindowStart = Time.unscaledTime;
    }

    // Mirror clears the diagnostics events in its own AfterSceneLoad hook, which can run after Awake: subscribing in
    // Start (the first frame) is safe, subscribing in Awake lost the subscription and the KB/s graphs stayed at zero.
    private void Start()
    {
        if (instance != this) return;

        NetworkDiagnostics.InMessageEvent -= OnIn;
        NetworkDiagnostics.OutMessageEvent -= OnOut;
        NetworkDiagnostics.InMessageEvent += OnIn;
        NetworkDiagnostics.OutMessageEvent += OnOut;
    }

    private void OnDestroy()
    {
        if (instance != this) return;

        NetworkDiagnostics.InMessageEvent -= OnIn;
        NetworkDiagnostics.OutMessageEvent -= OnOut;
        instance = null;
    }

    // Traffic per message type (RPC / command / spawn / sync...) so the heavy senders show up in the network panel.
    private readonly Dictionary<string, long> outByType = new Dictionary<string, long>();
    private readonly Dictionary<string, long> inByType = new Dictionary<string, long>();
    private string topOut = "", topIn = "";
    private float nextTopAt;

    private void OnIn(NetworkDiagnostics.MessageInfo info)
    {
        bytesIn += info.bytes * info.count; msgsIn += info.count;
        Count(inByType, info);
    }

    private void OnOut(NetworkDiagnostics.MessageInfo info)
    {
        bytesOut += info.bytes * info.count; msgsOut += info.count;
        Count(outByType, info);
    }

    private static void Count(Dictionary<string, long> table, NetworkDiagnostics.MessageInfo info)
    {
        string name = info.message != null ? info.message.GetType().Name : "?";
        long add = (long)info.bytes * info.count;
        table[name] = table.TryGetValue(name, out long previous) ? previous + add : add;
    }

    private static string Top(Dictionary<string, long> table, float seconds)
    {
        var sorted = new List<KeyValuePair<string, long>>(table);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
        table.Clear();

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Mathf.Min(4, sorted.Count); i++)
            sb.Append(sorted[i].Key).Append(' ').Append((sorted[i].Value / 1024f / seconds).ToString("0.0")).Append(" KB/s  ");
        return sb.ToString();
    }

    private void Update()
    {
        ReadKeys();
        SampleFrame();
        SampleNetwork();
    }

    private void ReadKeys()
    {
        // Headless test clients crash natively when the keyboard is touched.
        if (Application.isBatchMode) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // No keys of its own: the panels open and close together with the debug console (F1).
    }

    private void SampleFrame()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        frameMs[frameHead] = ms;
        frameHead = (frameHead + 1) % FrameSamples;
        frameCount = Mathf.Min(frameCount + 1, FrameSamples);

        if (Time.unscaledTime > worstResetAt)
        {
            worstResetAt = Time.unscaledTime + 5f;
            worstMs = 0f;
        }
        worstMs = Mathf.Max(worstMs, ms);

        if (ms >= SpikeMs && Time.frameCount > 10)
        {
            spikes.Add($"{DateTime.Now:HH:mm:ss}  {ms:0} ms  (scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name})");
            if (spikes.Count > SpikeLog) spikes.RemoveAt(0);
        }

        int gc = GC.CollectionCount(0);
        gcCollections += gc - lastGcCount;
        lastGcCount = gc;
        if (Time.unscaledTime - gcWindowStart >= 1f)
        {
            gcPerSecond = gcCollections;
            gcCollections = 0;
            gcWindowStart = Time.unscaledTime;
        }
    }

    private void SampleNetwork()
    {
        if (Time.unscaledTime >= nextTopAt)
        {
            nextTopAt = Time.unscaledTime + 2f;
            topOut = Top(outByType, 2f);
            topIn = Top(inByType, 2f);
        }

        if (Time.unscaledTime < nextNetSampleAt) return;
        float dt = NetInterval;
        nextNetSampleAt = Time.unscaledTime + NetInterval;

        float rttMs = NetworkClient.isConnected ? (float)(NetworkTime.rtt * 1000.0) : 0f;
        rtt[netHead] = rttMs;
        jitter[netHead] = NetworkClient.isConnected ? (float)(NetworkTime.rttVariance * 1000.0) : 0f;
        kbIn[netHead] = bytesIn / 1024f / dt;
        kbOut[netHead] = bytesOut / 1024f / dt;
        lastMsgsIn = msgsIn / dt;
        lastMsgsOut = msgsOut / dt;
        bytesIn = bytesOut = msgsIn = msgsOut = 0;

        rttMax = Mathf.Max(rttMax * 0.995f, rttMs);
        netHead = (netHead + 1) % NetSamples;
        netCount = Mathf.Min(netCount + 1, NetSamples);
    }

    // ------------------------------------------------------------------ drawing

    private void OnGUI()
    {
        showNet = showPerf = DebugConsoleMP.Instance != null && DebugConsoleMP.Instance.IsVisible;
        if (!NetworkClient.isConnected && !showNet) return;

        float scale = Mathf.Max(1f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;

        EnsureStyles();

        DrawPing(width);

        float y = 40f;
        if (showNet) y = DrawNetworkPanel(width, y);
        if (showPerf) DrawPerformancePanel(width, y);
    }

    private void EnsureStyles()
    {
        if (label != null) return;

        label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };
        label.normal.textColor = Color.white;
        title = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Bold };
    }

    // Top right, always while connected: our own round-trip time.
    private void DrawPing(float width)
    {
        if (!NetworkClient.isConnected) return;

        string text;
        Color color;

        if (NetworkServer.active)
        {
            text = "HOST  0 ms";
            color = new Color(0.55f, 0.85f, 1f);
        }
        else
        {
            float ms = (float)(NetworkTime.rtt * 1000.0);
            text = $"PING  {ms:0} ms";
            color = PingColor(ms);
        }

        var rect = new Rect(width - 190f, 6f, 180f, 28f);
        FillRect(rect, new Color(0f, 0f, 0f, 0.55f));
        var style = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }

    private float DrawNetworkPanel(float width, float y)
    {
        const float w = 420f;
        const float graphH = 48f;
        float x = 10f;
        float h = 30f + (graphH + 26f) * 3f + 82f;

        FillRect(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.7f));
        GUI.Label(new Rect(x + 8f, y + 4f, w, 20f), "NETWORK", title);

        float cy = y + 28f;
        float rttNow = netCount > 0 ? rtt[(netHead - 1 + NetSamples) % NetSamples] : 0f;
        float rttAvg = Average(rtt, netCount);

        GUI.Label(new Rect(x + 8f, cy, w, 18f),
            $"ping <b>{rttNow:0}</b> ms   avg {rttAvg:0}   max {rttMax:0}   jitter {Latest(jitter):0.0} ms", label);
        DrawBars(new Rect(x + 8f, cy + 20f, w - 16f, graphH), rtt, netHead, netCount, Mathf.Max(100f, rttMax * 1.15f), PingColor);
        cy += graphH + 26f;

        GUI.Label(new Rect(x + 8f, cy, w, 18f), $"in <b>{Latest(kbIn):0.0}</b> KB/s   {lastMsgsIn:0} msg/s", label);
        DrawBars(new Rect(x + 8f, cy + 20f, w - 16f, graphH), kbIn, netHead, netCount, Mathf.Max(10f, Max(kbIn)), _ => new Color(0.45f, 0.75f, 1f));
        cy += graphH + 26f;

        GUI.Label(new Rect(x + 8f, cy, w, 18f), $"out <b>{Latest(kbOut):0.0}</b> KB/s   {lastMsgsOut:0} msg/s", label);
        DrawBars(new Rect(x + 8f, cy + 20f, w - 16f, graphH), kbOut, netHead, netCount, Mathf.Max(10f, Max(kbOut)), _ => new Color(1f, 0.7f, 0.35f));
        cy += graphH + 26f;

        string table = NetworkPingMonitor_MP.LatestTable;
        var wrapped = new GUIStyle(label) { wordWrap = true, fontSize = 12 };
        GUI.Label(new Rect(x + 8f, cy, w - 16f, 80f),
            $"players: {(string.IsNullOrEmpty(table) ? "-" : table)}\nobjects: {NetworkClient.spawned.Count}   transport: {Transport.active?.GetType().Name}\nout: {topOut}\nin: {topIn}", wrapped);

        return y + h + 8f;
    }

    private void DrawPerformancePanel(float width, float y)
    {
        const float w = 420f;
        const float graphH = 70f;
        float x = 10f;
        float h = 30f + graphH + 26f + 44f + SpikeLog * 16f + 10f;

        FillRect(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.7f));
        GUI.Label(new Rect(x + 8f, y + 4f, w, 20f), "ANALYSIS", title);

        float avg = Average(frameMs, frameCount);
        float fps = avg > 0.01f ? 1000f / avg : 0f;
        float low = OnePercentLowFps();

        float cy = y + 28f;
        GUI.Label(new Rect(x + 8f, cy, w, 18f),
            $"frame <b>{avg:0.0}</b> ms   <b>{fps:0}</b> FPS   1% low {low:0}   worst(5s) {worstMs:0} ms", label);
        DrawBars(new Rect(x + 8f, cy + 20f, w - 16f, graphH), frameMs, frameHead, frameCount, 66f, FrameColor);

        // 16.6 ms and 33 ms guide lines
        DrawGuide(new Rect(x + 8f, cy + 20f, w - 16f, graphH), 16.6f, 66f);
        DrawGuide(new Rect(x + 8f, cy + 20f, w - 16f, graphH), 33.3f, 66f);
        cy += graphH + 26f;

        GUI.Label(new Rect(x + 8f, cy, w - 16f, 54f),
            $"GC collections/s: <b>{gcPerSecond}</b>   managed heap: {GC.GetTotalMemory(false) / 1048576f:0.0} MB   carry heals: <b>{CarriableObject_MP.HealCount}</b>\n" +
            $"scene objects: {FindObjectsCount()}   spikes (>{SpikeMs:0} ms) below, newest last:", label);
        cy += 40f;

        if (spikes.Count == 0)
        {
            GUI.Label(new Rect(x + 8f, cy, w - 16f, 18f), "  none yet", label);
            return;
        }

        foreach (string spike in spikes)
        {
            GUI.Label(new Rect(x + 8f, cy, w - 16f, 18f), "  " + spike, label);
            cy += 16f;
        }
    }

    private static int FindObjectsCount() => Time.frameCount % 60 == 0 || cachedObjects == 0
        ? cachedObjects = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length
        : cachedObjects;

    private static int cachedObjects;

    private float OnePercentLowFps()
    {
        if (frameCount < 20) return 0f;

        var sorted = new float[frameCount];
        Array.Copy(frameMs, sorted, frameCount);
        Array.Sort(sorted);

        int from = Mathf.Max(0, (int)(frameCount * 0.99f));
        float sum = 0f;
        int n = 0;
        for (int i = from; i < frameCount; i++) { sum += sorted[i]; n++; }

        float worstAvg = n > 0 ? sum / n : 0f;
        return worstAvg > 0.01f ? 1000f / worstAvg : 0f;
    }

    // ------------------------------------------------------------------ helpers

    private static Color PingColor(float ms)
    {
        if (ms < 60f) return new Color(0.40f, 0.90f, 0.45f);
        if (ms < 120f) return new Color(0.95f, 0.85f, 0.30f);
        if (ms < 200f) return new Color(1f, 0.60f, 0.25f);
        return new Color(0.95f, 0.35f, 0.35f);
    }

    private static Color FrameColor(float ms)
    {
        if (ms < 20f) return new Color(0.40f, 0.90f, 0.45f);
        if (ms < 34f) return new Color(0.95f, 0.85f, 0.30f);
        if (ms < SpikeMs) return new Color(1f, 0.60f, 0.25f);
        return new Color(0.95f, 0.30f, 0.30f);
    }

    private static void FillRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawGuide(Rect area, float value, float maxValue)
    {
        float y = area.yMax - Mathf.Clamp01(value / maxValue) * area.height;
        FillRect(new Rect(area.x, y, area.width, 1f), new Color(1f, 1f, 1f, 0.22f));
    }

    // Oldest sample on the left, newest on the right.
    private static void DrawBars(Rect area, float[] data, int head, int count, float maxValue, Func<float, Color> color)
    {
        FillRect(area, new Color(1f, 1f, 1f, 0.07f));
        if (count == 0 || maxValue <= 0f) return;

        float barWidth = area.width / data.Length;

        for (int i = 0; i < count; i++)
        {
            int index = (head - count + i + data.Length * 2) % data.Length;
            float value = data[index];
            float h = Mathf.Clamp01(value / maxValue) * area.height;
            if (h < 1f) h = 1f;

            float x = area.xMax - (count - i) * barWidth;
            FillRect(new Rect(x, area.yMax - h, Mathf.Max(1f, barWidth - 0.2f), h), color(value));
        }
    }

    private float Latest(float[] data) => netCount > 0 ? data[(netHead - 1 + NetSamples) % NetSamples] : 0f;

    private static float Max(float[] data)
    {
        float max = 0f;
        for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, data[i]);
        return max;
    }

    private static float Average(float[] data, int count)
    {
        if (count == 0) return 0f;

        float sum = 0f;
        for (int i = 0; i < count; i++) sum += data[i];
        return sum / count;
    }
}
