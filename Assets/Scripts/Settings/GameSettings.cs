using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Player options (audio, display, graphics). Stored in PlayerPrefs, separate from the save file,
// and applied automatically when the game starts and whenever a value changes.
public static class GameSettings
{
    public static readonly string[] WindowModeNames = { "Fullscreen", "Borderless", "Windowed" };
    public static readonly string[] OnOffNames = { "Off", "On" };
    public static readonly int[] FrameLimits = { 30, 60, 120, 144, -1 };
    public static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High" };
    public static readonly int[] MsaaSamples = { 1, 2, 4, 8 };
    public static readonly string[] MsaaNames = { "Off", "2x", "4x", "8x" };
    public static readonly float[] RenderScales = { 0.6f, 0.75f, 0.85f, 1f };

    public static event Action Changed;

    private const string MixerPath = "Audio/GameAudioMixer";
    private const string MusicParam = "MusicVolume";
    private const string SfxParam = "SfxVolume";
    private static AudioMixer mixer;
    private static AudioMixerGroup musicGroup;
    private static AudioMixerGroup sfxGroup;

    #region Values

    public static float MasterVolume { get => GetFloat("MasterVolume", 1f); set => SetFloat("MasterVolume", value); }
    public static float MusicVolume { get => GetFloat("MusicVolume", 0.8f); set => SetFloat("MusicVolume", value); }
    public static float SfxVolume { get => GetFloat("SfxVolume", 1f); set => SetFloat("SfxVolume", value); }

    public static int ResolutionIndex { get => ClampIndex(GetInt("Resolution", -1), Resolutions.Count, CurrentResolutionIndex()); set => SetInt("Resolution", value); }
    public static int WindowMode { get => ClampIndex(GetInt("WindowMode", 1), WindowModeNames.Length, 1); set => SetInt("WindowMode", value); }
    public static int VSync { get => ClampIndex(GetInt("VSync", 1), 2, 1); set => SetInt("VSync", value); }
    public static int FrameLimit { get => ClampIndex(GetInt("FrameLimit", 1), FrameLimits.Length, 1); set => SetInt("FrameLimit", value); }

    public static int Shadows { get => ClampIndex(GetInt("Shadows", 3), ShadowNames.Length, 3); set => SetInt("Shadows", value); }
    public static int AntiAliasing { get => ClampIndex(GetInt("AntiAliasing", 1), MsaaSamples.Length, 1); set => SetInt("AntiAliasing", value); }
    public static int AmbientOcclusion { get => ClampIndex(GetInt("AmbientOcclusion", 1), 2, 1); set => SetInt("AmbientOcclusion", value); }
    public static int RenderScale { get => ClampIndex(GetInt("RenderScale", 3), RenderScales.Length, 3); set => SetInt("RenderScale", value); }

    public static void ResetToDefaults()
    {
        foreach (var key in new[] { "MasterVolume", "MusicVolume", "SfxVolume", "Resolution", "WindowMode", "VSync", "FrameLimit", "Shadows", "AntiAliasing", "AmbientOcclusion", "RenderScale" })
        {
            PlayerPrefs.DeleteKey(Prefix + key);
        }
        PlayerPrefs.Save();
        ApplyAll();
        Changed?.Invoke();
    }

    private const string Prefix = "Settings.";
    private static float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);
    private static int GetInt(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
    private static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(value)); OnValueChanged(); }
    private static void SetInt(string key, int value) { PlayerPrefs.SetInt(Prefix + key, value); OnValueChanged(); }
    private static int ClampIndex(int value, int count, int fallback) => value >= 0 && value < count ? value : Mathf.Clamp(fallback, 0, Mathf.Max(0, count - 1));

    private static void OnValueChanged()
    {
        PlayerPrefs.Save();
        ApplyAll();
        Changed?.Invoke();
    }

    #endregion

    #region Resolutions

    private static List<Vector2Int> resolutions;
    public static List<Vector2Int> Resolutions
    {
        get
        {
            if (resolutions != null) return resolutions;
            resolutions = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(size)) resolutions.Add(size);
            }
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            return resolutions;
        }
    }
    private static int CurrentResolutionIndex()
    {
        var list = Resolutions;
        int index = list.IndexOf(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));
        return index >= 0 ? index : list.Count - 1;
    }
    public static string ResolutionName(int index)
    {
        var r = Resolutions[Mathf.Clamp(index, 0, Resolutions.Count - 1)];
        return r.x + " x " + r.y;
    }

    #endregion

    #region Apply

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
#if UNITY_EDITOR
        SnapshotEditorState();
#endif
        mixer = Resources.Load<AudioMixer>(MixerPath);
        if (mixer != null)
        {
            musicGroup = FindGroup("Music");
            sfxGroup = FindGroup("Sfx");
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        RouteAudioSources();
        ApplyDisplay();
        ApplyGraphics();
        // Mixer values set during the first frame can be ignored, so audio is applied once more a frame later.
        new GameObject("GameSettingsRunner").AddComponent<Runner>();
    }

    private static AudioMixerGroup FindGroup(string name)
    {
        var groups = mixer.FindMatchingGroups(name);
        return groups.Length > 0 ? groups[0] : null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RouteAudioSources();
        ApplyAudio();
    }

    public static void ApplyAll()
    {
        ApplyAudio();
        ApplyDisplay();
        ApplyGraphics();
    }

    // Sources without a mixer group go to Music (background music objects) or Effects (everything else).
    public static void RouteAudioSources()
    {
        if (mixer == null) return;
        foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (source.outputAudioMixerGroup != null) continue;
            source.outputAudioMixerGroup = source.name.IndexOf("Music", StringComparison.OrdinalIgnoreCase) >= 0 ? musicGroup : sfxGroup;
        }
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
        if (mixer == null) return;
        mixer.SetFloat(MusicParam, ToDecibel(MusicVolume));
        mixer.SetFloat(SfxParam, ToDecibel(SfxVolume));
    }

    private static float ToDecibel(float volume) => volume <= 0.0001f ? -80f : Mathf.Log10(volume) * 20f;

    public static void ApplyDisplay()
    {
        var mode = WindowMode == 0 ? FullScreenMode.ExclusiveFullScreen : WindowMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        var size = Resolutions[ResolutionIndex];
        // The editor's Game view ignores these; they matter in builds.
        if (!Application.isEditor && (Screen.width != size.x || Screen.height != size.y || Screen.fullScreenMode != mode))
        {
            Screen.SetResolution(size.x, size.y, mode);
        }

        QualitySettings.vSyncCount = VSync;
        Application.targetFrameRate = VSync == 1 ? -1 : FrameLimits[FrameLimit];
    }

    public static void ApplyGraphics()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;

        switch (Shadows)
        {
            case 0: urp.shadowDistance = 0f; break;
            case 1: urp.shadowDistance = 20f; urp.shadowCascadeCount = 1; break;
            case 2: urp.shadowDistance = 30f; urp.shadowCascadeCount = 2; break;
            default: urp.shadowDistance = 40f; urp.shadowCascadeCount = 2; break;
        }
        urp.msaaSampleCount = MsaaSamples[AntiAliasing];
        urp.renderScale = RenderScales[RenderScale];
        SetAmbientOcclusion(urp, AmbientOcclusion == 1);
    }

    private static void SetAmbientOcclusion(UniversalRenderPipelineAsset urp, bool enabled)
    {
        foreach (var feature in AmbientOcclusionFeatures(urp)) feature.SetActive(enabled);
    }

    private static List<ScriptableRendererFeature> AmbientOcclusionFeatures(UniversalRenderPipelineAsset urp)
    {
        var found = new List<ScriptableRendererFeature>();
        foreach (var data in urp.rendererDataList)
        {
            if (data == null) continue;
            foreach (var feature in data.rendererFeatures)
            {
                // The SSAO feature class is internal to URP, so it is matched by name.
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion") found.Add(feature);
            }
        }
        return found;
    }

    private class Runner : MonoBehaviour
    {
        private void Awake() => DontDestroyOnLoad(gameObject);
        private void Start() => ApplyAudio();
    }

    #endregion

#if UNITY_EDITOR
    // Graphics options write into the URP asset and quality settings, which are project files in the editor.
    // Remember the project's values on entering play mode and put them back on exit.
    private static float savedShadowDistance, savedRenderScale;
    private static int savedCascades, savedMsaa, savedVSync;
    private static bool savedAmbientOcclusion = true;

    private static void SnapshotEditorState()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            savedShadowDistance = urp.shadowDistance;
            savedCascades = urp.shadowCascadeCount;
            savedMsaa = urp.msaaSampleCount;
            savedRenderScale = urp.renderScale;
            foreach (var feature in AmbientOcclusionFeatures(urp)) savedAmbientOcclusion = feature.isActive;
        }
        savedVSync = QualitySettings.vSyncCount;
        EditorApplication.playModeStateChanged -= RestoreEditorState;
        EditorApplication.playModeStateChanged += RestoreEditorState;
    }

    private static void RestoreEditorState(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingPlayMode) return;
        EditorApplication.playModeStateChanged -= RestoreEditorState;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.shadowDistance = savedShadowDistance;
            urp.shadowCascadeCount = savedCascades;
            urp.msaaSampleCount = savedMsaa;
            urp.renderScale = savedRenderScale;
            SetAmbientOcclusion(urp, savedAmbientOcclusion);
        }
        QualitySettings.vSyncCount = savedVSync;
    }
#endif
}
