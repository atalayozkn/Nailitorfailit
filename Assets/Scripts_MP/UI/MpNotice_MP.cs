using System.Collections.Generic;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// On-screen notices for multiplayer problems that used to show up only in Player.log:
//   persistent: no internet, Steam not running / not signed in, Steam offline (can't reach Steam servers)
//   one-shot:   lobby create / join failures, could not connect, disconnected from the host
// Lives on its own DontDestroyOnLoad overlay canvas, so it works in the menus and in the match.
public class MpNotice_MP : MonoBehaviour
{
    public enum Kind { Info, Warning, Error }

    private const float CheckInterval = 1f;
    private const int MaxOneShot = 3;
    public const float ShownSeconds = 5f;
    private const float FadeSeconds = 0.6f;

    private static MpNotice_MP instance;

    private RectTransform stack;
    private readonly Dictionary<string, Row> persistent = new Dictionary<string, Row>();
    private readonly List<Row> oneShots = new List<Row>();
    private float nextCheck;
    private bool wasProblem;

    private class Row
    {
        public GameObject go;
        public TextMeshProUGUI text;
        public Image background;
        public CanvasGroup group;
        public float hideAt;
    }

    // A problem notice that was shown (or clicked away) is not shown again until the problem text changes or clears.
    private readonly Dictionary<string, string> dismissed = new Dictionary<string, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn() => Ensure();

    private static MpNotice_MP Ensure()
    {
        if (instance != null) return instance;

        var go = new GameObject("MpNotice_MP");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MpNotice_MP>();
        return instance;
    }

    // ---------------- public API ----------------

    public static void Error(string message, float seconds = ShownSeconds) => Ensure().ShowOneShot(message, Kind.Error, seconds);
    public static void Warning(string message, float seconds = ShownSeconds) => Ensure().ShowOneShot(message, Kind.Warning, seconds);
    public static void Info(string message, float seconds = ShownSeconds) => Ensure().ShowOneShot(message, Kind.Info, seconds);

    // Online multiplayer needs internet + a signed-in, connected Steam client. Returns a player-facing
    // reason when something is missing (used before hosting / joining, so we fail fast and clearly).
    public static bool CanUseSteamOnline(out string reason)
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            reason = Texts.NoInternet;
            return false;
        }

        if (!SteamManager.Initialized)
        {
            reason = Texts.SteamNotRunning;
            return false;
        }

        if (!SteamUser.BLoggedOn())
        {
            reason = Texts.SteamOffline;
            return false;
        }

        reason = null;
        return true;
    }

    public static class Texts
    {
        public const string NoInternet = "No internet connection. Online multiplayer needs internet - check your network.";
        public const string SteamNotRunning = "Steam is not running or you are not signed in. Start Steam, sign in, then restart the game.";
        public const string SteamOffline = "Steam is offline - it can't reach the Steam servers. Check your connection or turn off Steam's offline mode.";
        public const string BackOnline = "Connection restored - you are back online.";
    }

    // ---------------- status monitor ----------------

    private void Update()
    {
        float now = Time.unscaledTime;

        for (int i = oneShots.Count - 1; i >= 0; i--)
        {
            if (!TickRow(oneShots[i], now)) continue;
            oneShots.RemoveAt(i);
        }

        List<string> finished = null;
        foreach (KeyValuePair<string, Row> pair in persistent)
        {
            if (!TickRow(pair.Value, now)) continue;
            (finished ??= new List<string>()).Add(pair.Key);
        }

        if (finished != null)
        {
            foreach (string key in finished)
            {
                dismissed[key] = persistent[key].text.text;
                persistent.Remove(key);
            }
        }

        if (now < nextCheck) return;
        nextCheck = now + CheckInterval;

        // Only where multiplayer is in play, and not in the local KCP dev mode (no Steam needed there).
        bool relevant = NetworkManager.singleton is LobbyNetworkManager && !LobbyNetworkManager.ForceDevTransport;

        bool problem = false;
        string reason = null;
        if (relevant) problem = !CanUseSteamOnline(out reason);

        SetPersistent("status", problem ? reason : null, Kind.Error);

        if (wasProblem && !problem && relevant) Info(Texts.BackOnline);
        wasProblem = problem;
    }

    // ---------------- UI ----------------

    // Fades the notice out over its last moments and removes it. True once it is gone.
    private static bool TickRow(Row row, float now)
    {
        if (row.go == null) return true;

        float left = row.hideAt - now;
        if (left <= 0f)
        {
            Destroy(row.go);
            return true;
        }

        if (row.group != null) row.group.alpha = Mathf.Clamp01(left / FadeSeconds);
        return false;
    }

    private static void Refresh(Row row, float seconds)
    {
        row.hideAt = Time.unscaledTime + seconds;
        if (row.group != null) row.group.alpha = 1f;
    }

    private void ShowOneShot(string message, Kind kind, float seconds)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        message = Localization.T(message); // the rows hold the text in the current language

        // Same text already on screen: just extend it instead of stacking duplicates.
        foreach (Row existing in oneShots)
        {
            if (existing.text.text != message) continue;
            Refresh(existing, seconds);
            return;
        }
        foreach (Row p in persistent.Values)
        {
            if (p.text.text != message) continue;
            Refresh(p, seconds); // pressing Create / Join again brings the same problem back on screen
            return;
        }

        if (oneShots.Count >= MaxOneShot)
        {
            Destroy(oneShots[0].go);
            oneShots.RemoveAt(0);
        }

        Row row = CreateRow(message, kind);
        row.hideAt = Time.unscaledTime + seconds;
        oneShots.Add(row);

        Debug.Log($"[MP-NOTICE] {kind}: {message}");
    }

    private void SetPersistent(string key, string message, Kind kind)
    {
        message = Localization.T(message);
        persistent.TryGetValue(key, out Row row);

        if (string.IsNullOrEmpty(message))
        {
            dismissed.Remove(key);
            if (row == null) return;
            Destroy(row.go);
            persistent.Remove(key);
            return;
        }

        if (row != null && row.text.text == message) return;
        if (row == null && dismissed.TryGetValue(key, out string seen) && seen == message) return; // already shown once
        if (row != null) Destroy(row.go);

        row = CreateRow(message, kind);
        row.hideAt = Time.unscaledTime + ShownSeconds;
        row.go.transform.SetAsFirstSibling(); // persistent problems on top
        persistent[key] = row;

        Debug.LogWarning($"[MP-NOTICE] persistent: {message}");
    }

    // Same look as the menu: the opaque brown plate, a coloured edge for the kind, cream text in the game font.
    private static Sprite plateSprite;
    private static TMP_FontAsset gameFont;

    private static readonly Color Cream = new Color(1f, 0.90f, 0.70f, 1f);

    private static Color AccentOf(Kind kind) => kind == Kind.Error ? new Color(0.93f, 0.33f, 0.30f, 1f)
                                              : kind == Kind.Warning ? new Color(0.99f, 0.77f, 0.26f, 1f)
                                              : new Color(0.45f, 0.88f, 0.45f, 1f);

    private Row CreateRow(string message, Kind kind)
    {
        EnsureCanvas();

        if (plateSprite == null) plateSprite = Resources.Load<Sprite>("Menu/MenuButtons_Panel");
        if (gameFont == null)
        {
            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font.name.StartsWith("DynaPuff")) { gameFont = font; break; }
        }

        var go = new GameObject("Notice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button),
                                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(stack, false);

        var background = go.GetComponent<Image>();
        background.sprite = plateSprite;
        background.type = plateSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = plateSprite != null ? Color.white : new Color(0.35f, 0.24f, 0.16f, 1f);
        go.GetComponent<LayoutElement>().minHeight = 84f;

        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(30, 34, 18, 18);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Icon: "!" in the colour of the kind ("i" for plain information).
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(LayoutElement));
        iconGo.transform.SetParent(go.transform, false);
        var iconLayout = iconGo.GetComponent<LayoutElement>();
        iconLayout.minWidth = iconLayout.preferredWidth = 22f;
        iconLayout.minHeight = iconLayout.preferredHeight = 44f;

        var icon = iconGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) icon.font = gameFont;
        icon.text = kind == Kind.Info ? "i" : "!";
        icon.fontSize = 46f;
        icon.fontStyle = FontStyles.Bold;
        icon.alignment = TextAlignmentOptions.Center;
        icon.color = AccentOf(kind);
        icon.outlineWidth = 0.22f;
        icon.outlineColor = new Color32(34, 21, 14, 255);
        icon.textWrappingMode = TextWrappingModes.NoWrap;
        icon.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
        textGo.transform.SetParent(go.transform, false);
        textGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (gameFont != null) text.font = gameFont;
        else if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = message;
        text.fontSize = 23f;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Cream;
        text.raycastTarget = false;
        TextOutline.Attach(text, 1.3f, Color.black); // a black outline: readable on any plate and background

        var row = new Row { go = go, text = text, background = background, group = go.GetComponent<CanvasGroup>() };

        // Click it away.
        go.GetComponent<Button>().onClick.AddListener(() => row.hideAt = Time.unscaledTime - 0.01f);

        return row;
    }

    private void EnsureCanvas()
    {
        if (stack != null) return;

        var canvasGo = new GameObject("MpNoticeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var stackGo = new GameObject("Stack", typeof(RectTransform), typeof(VerticalLayoutGroup));
        stackGo.transform.SetParent(canvasGo.transform, false);
        stack = (RectTransform)stackGo.transform;
        // Middle of the right edge.
        stack.anchorMin = new Vector2(1f, 0.5f);
        stack.anchorMax = new Vector2(1f, 0.5f);
        stack.pivot = new Vector2(1f, 0.5f);
        stack.anchoredPosition = new Vector2(-28f, 0f);
        stack.sizeDelta = new Vector2(560f, 0f);

        var layout = stackGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        stackGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
