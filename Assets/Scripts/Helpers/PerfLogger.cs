#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Development builds only: writes one CSV row per frame to persistentDataPath/PerfLogs. F9 drops a marker.
public class PerfLogger : MonoBehaviour
{
    const float FlushInterval = 2f;

    readonly StringBuilder buffer = new StringBuilder(1 << 16);
    readonly FrameTiming[] timings = new FrameTiming[1];
    StreamWriter writer;
    float lastFlush;
    int markerCount;
    string pendingMarker = "";
    string sceneName = "";

    ProfilerRecorder gcAlloc, usedMemory, drawCalls, batches, setPass, triangles;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        var go = new GameObject("[PerfLogger]");
        DontDestroyOnLoad(go);
        go.AddComponent<PerfLogger>();
    }

    void Awake()
    {
        UncapFrameRate();

        string dir = Path.Combine(Application.persistentDataPath, "PerfLogs");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "perf_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        writer = new StreamWriter(path, false, new UTF8Encoding(false));

        var res = Screen.currentResolution;
        writer.WriteLine("# cpu=" + SystemInfo.processorType + " cores=" + SystemInfo.processorCount);
        writer.WriteLine("# gpu=" + SystemInfo.graphicsDeviceName + " vram=" + SystemInfo.graphicsMemorySize + "MB api=" + SystemInfo.graphicsDeviceType);
        writer.WriteLine("# ram=" + SystemInfo.systemMemorySize + "MB os=" + SystemInfo.operatingSystem);
        writer.WriteLine("# screen=" + Screen.width + "x" + Screen.height + " refresh=" + res.refreshRateRatio.value.ToString("0.##", CultureInfo.InvariantCulture) + " mode=" + Screen.fullScreenMode + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        writer.WriteLine("# unity=" + Application.unityVersion + " version=" + Application.version + " frameTimingStats=" + FrameTimingManager.IsFeatureEnabled());
        writer.WriteLine("time,frame,scene,frameMs,cpuMs,cpuMainMs,cpuRenderMs,gpuMs,gcAllocKB,usedMemMB,drawCalls,batches,setPass,trisK,marker");
        Debug.Log("[PerfLogger] writing " + path);

        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        usedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");

        SceneManager.activeSceneChanged += OnSceneChanged;
        sceneName = SceneManager.GetActiveScene().name;
    }

    void OnSceneChanged(Scene from, Scene to)
    {
        sceneName = to.name;
        pendingMarker += "SCENE:" + to.name + " ";
        UncapFrameRate();
    }

    static void UncapFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame)
        {
            markerCount++;
            pendingMarker += "MARK" + markerCount + " ";
            Debug.Log("[PerfLogger] marker " + markerCount + " at " + Time.realtimeSinceStartup.ToString("0.00", CultureInfo.InvariantCulture));
        }
    }

    void LateUpdate()
    {
        FrameTimingManager.CaptureFrameTimings();
        double cpu = 0, cpuMain = 0, cpuRender = 0, gpu = 0;
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            cpu = timings[0].cpuFrameTime;
            cpuMain = timings[0].cpuMainThreadFrameTime;
            cpuRender = timings[0].cpuRenderThreadFrameTime;
            gpu = timings[0].gpuFrameTime;
        }

        var ci = CultureInfo.InvariantCulture;
        buffer.Append(Time.realtimeSinceStartup.ToString("0.000", ci)).Append(',')
              .Append(Time.frameCount).Append(',')
              .Append(sceneName).Append(',')
              .Append((Time.unscaledDeltaTime * 1000f).ToString("0.00", ci)).Append(',')
              .Append(cpu.ToString("0.00", ci)).Append(',')
              .Append(cpuMain.ToString("0.00", ci)).Append(',')
              .Append(cpuRender.ToString("0.00", ci)).Append(',')
              .Append(gpu.ToString("0.00", ci)).Append(',')
              .Append((gcAlloc.LastValue / 1024.0).ToString("0.0", ci)).Append(',')
              .Append(usedMemory.LastValue / (1024 * 1024)).Append(',')
              .Append(drawCalls.LastValue).Append(',')
              .Append(batches.LastValue).Append(',')
              .Append(setPass.LastValue).Append(',')
              .Append(triangles.LastValue / 1000).Append(',')
              .Append(pendingMarker.Trim()).Append('\n');
        pendingMarker = "";

        if (Time.realtimeSinceStartup - lastFlush > FlushInterval)
            Flush();
    }

    void Flush()
    {
        lastFlush = Time.realtimeSinceStartup;
        if (writer == null || buffer.Length == 0) return;
        writer.Write(buffer);
        writer.Flush();
        buffer.Clear();
    }

    void OnApplicationQuit() => Close();
    void OnDestroy() => Close();

    void Close()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        Flush();
        writer?.Dispose();
        writer = null;
        gcAlloc.Dispose(); usedMemory.Dispose(); drawCalls.Dispose();
        batches.Dispose(); setPass.Dispose(); triangles.Dispose();
    }
}
#endif
