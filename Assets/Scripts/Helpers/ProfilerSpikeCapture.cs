#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// Development builds only, and only when "spikecapture.txt" sits next to the exe.
// Records the profiler in short segments and keeps only the segments that contain a hitch,
// so the files stay small and each one opens in the editor Profiler.
public class ProfilerSpikeCapture : MonoBehaviour
{
    const int SegmentFrames = 900;
    const float SpikeMs = 50f;
    const int IgnoreFramesAfterLoad = 90;

    string dir;
    int segmentIndex;
    int segmentStartFrame;
    int ignoreUntilFrame;
    bool currentHasSpike;
    readonly List<(string path, bool keep)> closed = new List<(string, bool)>();
    string currentPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        string flag = Path.Combine(Path.GetDirectoryName(Application.dataPath), "spikecapture.txt");
        if (!File.Exists(flag)) return;
        var go = new GameObject("[ProfilerSpikeCapture]");
        DontDestroyOnLoad(go);
        go.AddComponent<ProfilerSpikeCapture>();
    }

    void Awake()
    {
        dir = Path.Combine(Application.persistentDataPath, "PerfLogs", "Spikes_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dir);
        Profiler.maxUsedMemory = 256 * 1024 * 1024;
        Profiler.enabled = true;
        SceneManager.activeSceneChanged += (a, b) => ignoreUntilFrame = Time.frameCount + IgnoreFramesAfterLoad;
        ignoreUntilFrame = Time.frameCount + IgnoreFramesAfterLoad;
        StartSegment();
        Debug.Log("[ProfilerSpikeCapture] recording to " + dir);
    }

    void StartSegment()
    {
        segmentIndex++;
        currentPath = Path.Combine(dir, "seg_" + segmentIndex.ToString("0000") + "_" + SceneManager.GetActiveScene().name);
        Profiler.enableBinaryLog = false;
        Profiler.logFile = currentPath;
        Profiler.enableBinaryLog = true;
        segmentStartFrame = Time.frameCount;
        currentHasSpike = false;
    }

    void Update()
    {
        if (Time.frameCount > ignoreUntilFrame && Time.unscaledDeltaTime * 1000f > SpikeMs)
        {
            currentHasSpike = true;
            if (Time.frameCount - segmentStartFrame < 3 && closed.Count > 0)
                closed[closed.Count - 1] = (closed[closed.Count - 1].path, true);
            File.AppendAllText(Path.Combine(dir, "spikes.txt"),
                Path.GetFileName(currentPath) + " frame=" + Time.frameCount + " ms=" + (Time.unscaledDeltaTime * 1000f).ToString("0") + "\n");
        }

        if (Time.frameCount - segmentStartFrame < SegmentFrames) return;

        closed.Add((currentPath, currentHasSpike));
        StartSegment();

        while (closed.Count > 2)
        {
            var old = closed[0];
            closed.RemoveAt(0);
            if (old.keep) continue;
            try { File.Delete(old.path + ".raw"); } catch { }
        }
    }

    void OnApplicationQuit()
    {
        Profiler.enableBinaryLog = false;
        Profiler.enabled = false;
    }
}
#endif
