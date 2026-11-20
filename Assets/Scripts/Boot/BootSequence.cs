using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// First scene of the game (BootScene, build index 0):
//   1. Loading screen   - background, centre image/title, spinning tools icon + "Loading..." bottom-right.
//                          Fades in (1 s) while the main menu loads in the background, fades out (1 s) when ready.
//   2. Logos            - Poder Games, then Unity: each fades in (1 s), holds, fades out (1 s).
//   3. Main menu        - the preloaded scene is activated.
// All UI is built in code; only the textures are assigned on the component.
public class BootSequence : MonoBehaviour
{
    [Header("Next scene")]
    [SerializeField] private string nextScene = "MenuScene";

    [Header("Loading screen")]
    [SerializeField] private Texture background;
    [SerializeField] private Color backgroundTint = Color.white;
    [Tooltip("Visible part of the background picture (min x/y, max x/y in 0-1 texture fractions). Cuts the transparent / wavy sky border of the artwork.")]
    [SerializeField] private Vector4 backgroundCrop = new Vector4(0.052f, 0.018f, 0.923f, 0.960f);
    [Tooltip("Layer between the background and the centre picture (19.png: folder + clipboard). Fitted to the screen, aspect kept.")]
    [SerializeField] private Texture overlay;
    [Tooltip("Size of the folder/clipboard picture relative to the fitted size (1 = fills the screen). Centred.")]
    [SerializeField, Range(0.3f, 1f)] private float overlayScale = 0.8f;
    [Tooltip("Visible part of the overlay picture (min x/y, max x/y in 0-1 texture fractions). The rest (transparent margin) is cropped away so the folder fills the screen. Default = whole picture.")]
    [SerializeField] private Vector4 overlayCrop = new Vector4(0.1359f, 0.0264f, 0.8934f, 0.9250f);
    [Tooltip("Where the centre picture sits ON the overlay, in the overlay picture's fractions (min x/y, max x/y). " +
             "Default = the white paper of the clipboard in 19.png (paper: x 0.557-0.852, y 0.103-0.848).")]
    [SerializeField] private Vector4 centerAreaOnOverlay = new Vector4(0.58f, 0.15f, 0.83f, 0.80f);
    [Tooltip("Centre picture (game logo / key art). Empty = the title text below is shown instead.")]
    [SerializeField] private Texture centerImage;
    [Tooltip("Where the centre picture sits, in screen fractions. (0.75, 0.5) = middle of the right half.")]
    [SerializeField] private Vector2 centerAnchor = new Vector2(0.75f, 0.5f);
    [SerializeField] private Vector2 centerSize = new Vector2(600f, 600f);
    [SerializeField] private string titleText = "Nail It Or Fail It";
    [Tooltip("Icon that spins next to the 'Loading' text (frame_00002: crossed wrench + screwdriver).")]
    [SerializeField] private Texture spinnerIcon;
    [SerializeField] private float spinDegreesPerSecond = 180f;
    [SerializeField] private string loadingText = "Loading";
    [Tooltip("Spinner + 'Loading...' colour. Light, shown straight on the background.")]
    [SerializeField] private Color indicatorColor = new Color(1f, 0.96f, 0.88f, 1f);

    [Header("Logos (shown one after another)")]
    [SerializeField] private Texture studioLogo;
    [Tooltip("Official 'Made with Unity' logo. Empty = a text fallback is shown.")]
    [SerializeField] private Texture unityLogo;
    [SerializeField] private Color logoBackground = Color.black;

    [Header("Timing (seconds)")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float logoHold = 1.5f;
    [Tooltip("Keep the loading screen at least this long, so it doesn't flash on fast machines.")]
    [SerializeField] private float minLoadingTime = 1f;

    // Videos live in Resources/Boot (loaded by name, no scene wiring needed).
    private const string LoadingVideoName = "Boot/hammers";          // looping hammer pattern behind the loading screen
    private const string StudioVideoName = "Boot/poder_games";       // "Poder Games" typed with nail sounds, under the studio logo

    private VideoPlayer loadingVideo;
    private RenderTexture loadingVideoTexture;
    private CanvasGroup loadingGroup;
    private RectTransform spinner;
    private TextMeshProUGUI loadingLabel;
    private float spinnerTime;

    private IEnumerator Start()
    {
        BuildCamera();
        Canvas canvas = BuildCanvas();
        loadingGroup = BuildLoadingScreen(canvas.transform);

        Debug.Log("[Boot] loading screen fade-in");
        yield return Fade(loadingGroup, 0f, 1f);

        // Load the menu only once the screen is fully visible, at low priority so the spinner keeps animating.
        // It is activated after the logos.
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        AsyncOperation load = SceneManager.LoadSceneAsync(nextScene);
        if (load != null) load.allowSceneActivation = false;
        else Debug.LogError($"[Boot] Scene '{nextScene}' is not in the build settings.");

        float shown = 0f;
        while ((load != null && load.progress < 0.9f) || shown < minLoadingTime)
        {
            shown += Step;
            yield return null;
        }
        Debug.Log($"[Boot] '{nextScene}' loaded (screen shown {shown:0.0}s) - fade-out");

        yield return Fade(loadingGroup, 1f, 0f);
        loadingGroup.gameObject.SetActive(false);
        StopLoadingVideo();

        yield return ShowLogo(canvas.transform, studioLogo, "Poder Games", Resources.Load<VideoClip>(StudioVideoName));
        yield return ShowLogo(canvas.transform, unityLogo, "Made with Unity");

        Application.backgroundLoadingPriority = ThreadPriority.Normal;
        Debug.Log("[Boot] logos done - opening the main menu");
        if (load != null) load.allowSceneActivation = true;
    }

    // Frame time capped at 50 ms: scene loading causes multi-second frames in the Editor, and with the raw delta
    // a whole fade (or the minimum loading time) "elapsed" in one frame - the loading screen was never seen.
    private static float Step => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

    private void Update()
    {
        if (spinner == null || loadingGroup == null || !loadingGroup.gameObject.activeSelf) return;

        spinnerTime += Step;
        spinner.localRotation = Quaternion.Euler(0f, 0f, -spinDegreesPerSecond * spinnerTime); // clockwise

        int dots = (int)(spinnerTime / 0.4f) % 4;
        if (loadingLabel != null) loadingLabel.text = loadingText + new string('.', dots);
    }

    // ------------------------------------------------------------------ building the UI

    private static void BuildCamera()
    {
        var cam = new GameObject("BootCamera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        cam.depth = -100;
        cam.gameObject.AddComponent<AudioListener>(); // the studio video's sound needs a listener (there is no other in this scene)
    }

    private static Canvas BuildCanvas()
    {
        var go = new GameObject("BootCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private CanvasGroup BuildLoadingScreen(Transform parent)
    {
        RectTransform root = Rect("LoadingScreen", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        RawImage bg = Raw("Background", root, background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.color = background != null ? backgroundTint : new Color(0.13f, 0.12f, 0.12f, 1f);
        if (background != null)
        {
            // Fill the screen without stretching (crops the edges on other aspect ratios).
            var envelope = bg.gameObject.AddComponent<AspectRatioFitter>();
            envelope.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            float bw = Mathf.Max(0.05f, backgroundCrop.z - backgroundCrop.x), bh = Mathf.Max(0.05f, backgroundCrop.w - backgroundCrop.y);
            bg.uvRect = new Rect(backgroundCrop.x, backgroundCrop.y, bw, bh);
            envelope.aspectRatio = background.height > 0 ? (background.width * bw) / (background.height * bh) : 16f / 9f;
        }

        StartLoadingVideo(root, bg);

        // Middle layer: background -> overlay -> centre picture.
        RawImage layer = null;
        if (overlay != null)
        {
            layer = Raw("Overlay", root, overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            float cw = Mathf.Max(0.05f, overlayCrop.z - overlayCrop.x), ch = Mathf.Max(0.05f, overlayCrop.w - overlayCrop.y);
            layer.uvRect = new Rect(overlayCrop.x, overlayCrop.y, cw, ch);
            Fit(layer, overlay, (overlay.width * cw) / Mathf.Max(1f, overlay.height * ch));
            layer.rectTransform.parent.localScale = Vector3.one * overlayScale; // the frame is centred: scales about the middle
        }

        // Centre: picture if assigned, otherwise the game title. With an overlay it is placed on the overlay
        // picture itself (the clipboard paper), so it stays there at any screen aspect.
        if (centerImage != null)
        {
            // centerAreaOnOverlay is in whole-picture fractions; remap into the cropped view.
            float cw2 = Mathf.Max(0.05f, overlayCrop.z - overlayCrop.x), ch2 = Mathf.Max(0.05f, overlayCrop.w - overlayCrop.y);
            Vector2 areaMin = new Vector2((centerAreaOnOverlay.x - overlayCrop.x) / cw2, (centerAreaOnOverlay.y - overlayCrop.y) / ch2);
            Vector2 areaMax = new Vector2((centerAreaOnOverlay.z - overlayCrop.x) / cw2, (centerAreaOnOverlay.w - overlayCrop.y) / ch2);
            RawImage center = layer != null
                ? Raw("CenterImage", layer.rectTransform, centerImage, areaMin, areaMax, Vector2.zero, Vector2.zero)
                : Raw("CenterImage", root, centerImage, centerAnchor, centerAnchor, Vector2.zero, centerSize);
            Fit(center, centerImage);
        }
        else
        {
            TextMeshProUGUI title = Text("Title", root, titleText, 96f, centerAnchor, new Vector2(0f, 20f),
                                         new Vector2(1400f, 200f), TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
        }

        // Bottom-right: spinning icon + "Loading..."
        RectTransform corner = Rect("LoadingIndicator", root, new Vector2(1f, 0f), new Vector2(1f, 0f),
                                    new Vector2(-170f, 90f), new Vector2(280f, 100f));
        if (spinnerIcon != null)
        {
            RawImage icon = Raw("Spinner", corner, spinnerIcon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                new Vector2(50f, 0f), new Vector2(72f, 72f));
            icon.color = indicatorColor;
            spinner = icon.rectTransform; // pivot stays at the centre: spins in place
        }
        loadingLabel = Text("LoadingText", corner, loadingText, 30f, new Vector2(0f, 0.5f), new Vector2(195f, 0f),
                            new Vector2(200f, 50f), TextAlignmentOptions.Left);
        loadingLabel.color = indicatorColor;
        loadingLabel.fontStyle = FontStyles.Bold;

        // Black cover on top does the fade: fading the CanvasGroup made every layer translucent, so the
        // background showed through the 19.png folder during the fade.
        Image cover = Rect(FadeCoverName, root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        cover.color = Color.black;
        cover.raycastTarget = false;
        return group;
    }

    private IEnumerator ShowLogo(Transform parent, Texture logo, string fallback, VideoClip video = null)
    {
        RectTransform root = Rect("Logo_" + fallback, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        RawImage bg = Raw("Background", root, null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        bg.color = logoBackground;

        // With a video the logo sits in the upper half and the video text appears below it.
        VideoPlayer player = null;
        RenderTexture videoTexture = null;
        if (video != null) player = BuildLogoVideo(root, video, out videoTexture);

        // Logo above, video text below, the pair centred as a whole on the screen.
        Vector2 logoPosition = player != null ? new Vector2(0f, 90f) : Vector2.zero;
        Vector2 logoSize = player != null ? new Vector2(440f, 440f) : new Vector2(800f, 450f);

        if (logo != null)
        {
            RawImage image = Raw("Logo", root, logo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 logoPosition, logoSize);
            Fit(image, logo);
        }
        else
        {
            Text("LogoText", root, fallback, 72f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 200f),
                 TextAlignmentOptions.Center);
        }

        Debug.Log($"[Boot] logo '{fallback}' ({(logo != null ? logo.name : "text fallback")})");
        if (player != null) player.Prepare(); // decode while the logo fades in
        yield return Fade(group, 0f, 1f);

        if (player != null)
        {
            // Wait for the video (max 3 s), then play it to the end.
            for (float t = 0f; !player.isPrepared && !videoFailed && t < 3f; t += Step) yield return null;

            if (player.isPrepared && !videoFailed)
            {
                videoDone = false;
                Debug.Log($"[Boot] studio video playing ({player.length:0.0}s, audio tracks {player.audioTrackCount}).");
                float started = Time.realtimeSinceStartup;
                player.Play();
                for (float t = 0f; !videoDone && !videoFailed && t < player.length + 2f; t += Step) yield return null;
                Debug.Log($"[Boot] studio video finished after {Time.realtimeSinceStartup - started:0.0}s (done={videoDone}).");
            }
            else Debug.LogWarning("[Boot] studio video not ready - skipped.");
        }

        for (float t = 0f; t < logoHold; t += Step) yield return null;
        yield return Fade(group, 1f, 0f);

        if (player != null) player.Stop();
        if (videoTexture != null) videoTexture.Release();
        Destroy(root.gameObject);
    }

    private bool videoDone, videoFailed;

    // Full-screen video (black background, so it blends into the black logo screen). Audio goes through an AudioSource.
    private VideoPlayer BuildLogoVideo(RectTransform root, VideoClip clip, out RenderTexture texture)
    {
        videoDone = false;
        videoFailed = false;

        texture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "BootStudioVideo" };
        // The video's text sits at the middle of its frame: the frame is moved down so the text lands under the logo.
        RawImage image = Raw("Video", root, texture, Vector2.zero, Vector2.one, new Vector2(0f, -260f), Vector2.zero);
        Fit(image, texture); // own frame keeps the offset: an AspectRatioFitter on the image itself would reset it

        var go = new GameObject("StudioVideoPlayer");
        go.transform.SetParent(root, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 1f;

        var player = go.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.controlledAudioTrackCount = 1;
        player.SetTargetAudioSource(0, source);
        player.loopPointReached += _ => videoDone = true;
        player.errorReceived += (_, message) => { videoFailed = true; Debug.LogWarning("[Boot] studio video error: " + message); };
        return player;
    }

    // Looping hammer pattern replaces the still background once its first frame is ready (the still art shows until then).
    private void StartLoadingVideo(RectTransform root, RawImage background)
    {
        VideoClip clip = Resources.Load<VideoClip>(LoadingVideoName);
        if (clip == null) return;

        loadingVideoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "BootLoadingVideo" };

        var go = new GameObject("LoadingVideoPlayer");
        go.transform.SetParent(root, false);
        loadingVideo = go.AddComponent<VideoPlayer>();
        loadingVideo.playOnAwake = false;
        loadingVideo.isLooping = true;
        loadingVideo.skipOnDrop = true;
        loadingVideo.clip = clip;
        loadingVideo.renderMode = VideoRenderMode.RenderTexture;
        loadingVideo.targetTexture = loadingVideoTexture;
        loadingVideo.audioOutputMode = VideoAudioOutputMode.None;
        loadingVideo.sendFrameReadyEvents = true;
        loadingVideo.errorReceived += (_, message) => Debug.LogWarning("[Boot] loading video error: " + message);
        loadingVideo.prepareCompleted += p => p.Play();

        bool shown = false;
        loadingVideo.frameReady += (p, _) =>
        {
            if (shown || background == null) return;
            shown = true;

            background.texture = loadingVideoTexture;
            background.uvRect = new Rect(0f, 0f, 1f, 1f);
            background.color = Color.white;
            var fit = background.GetComponent<AspectRatioFitter>();
            if (fit == null) fit = background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;
            Debug.Log("[Boot] loading video playing as the background.");
        };

        loadingVideo.Prepare();
    }

    private void StopLoadingVideo()
    {
        if (loadingVideo != null) loadingVideo.Stop();
        if (loadingVideoTexture != null) loadingVideoTexture.Release();
    }

    private const string FadeCoverName = "FadeCover";

    // Fades a screen in/out. Screens with a FadeCover child fade by the cover's opacity (layers stay opaque);
    // others fade the whole CanvasGroup.
    private IEnumerator Fade(CanvasGroup group, float from, float to)
    {
        Transform coverTransform = group.transform.Find(FadeCoverName);
        Image cover = coverTransform != null ? coverTransform.GetComponent<Image>() : null;
        if (cover != null) group.alpha = 1f;

        for (float t = 0f; t < fadeDuration; t += Step)
        {
            Apply(group, cover, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        Apply(group, cover, to);
    }

    private static void Apply(CanvasGroup group, Image cover, float visibility)
    {
        if (cover != null) cover.color = new Color(0f, 0f, 0f, 1f - visibility);
        else group.alpha = visibility;
    }

    // ------------------------------------------------------------------ small UI helpers

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                      Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RawImage Raw(string name, Transform parent, Texture texture, Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 position, Vector2 size)
    {
        RawImage image = Rect(name, parent, anchorMin, anchorMax, position, size).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    // Keep the picture's aspect ratio inside its box. The box values are read BEFORE the AspectRatioFitter is
    // added: adding it immediately stretches the image to its parent (the whole screen), which made the logos
    // fill the screen.
    private static void Fit(RawImage image, Texture texture, float aspectOverride = 0f)
    {
        RectTransform rect = image.rectTransform;
        Vector2 anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, position = rect.anchoredPosition, box = rect.sizeDelta;
        Transform parent = rect.parent;

        RectTransform frame = Rect(image.name + "Frame", parent, anchorMin, anchorMax, position, box);
        rect.SetParent(frame, false);

        var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = aspectOverride > 0f ? aspectOverride : (texture.height > 0 ? (float)texture.width / texture.height : 1f);
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Vector2 anchor,
                                        Vector2 position, Vector2 box, TextAlignmentOptions align)
    {
        var text = Rect(name, parent, anchor, anchor, position, box).gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
