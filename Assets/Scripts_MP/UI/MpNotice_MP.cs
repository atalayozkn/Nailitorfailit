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
        public float hideAt; // 0 = persistent
    }

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

    public static void Error(string message, float seconds = 7f) => Ensure().ShowOneShot(message, Kind.Error, seconds);
    public static void Warning(string message, float seconds = 6f) => Ensure().ShowOneShot(message, Kind.Warning, seconds);
    public static void Info(string message, float seconds = 4f) => Ensure().ShowOneShot(message, Kind.Info, seconds);

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
            if (now < oneShots[i].hideAt) continue;
            Destroy(oneShots[i].go);
            oneShots.RemoveAt(i);
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

    private void ShowOneShot(string message, Kind kind, float seconds)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        // Same text already on screen: just extend it instead of stacking duplicates.
        foreach (Row existing in oneShots)
        {
            if (existing.text.text != message) continue;
            existing.hideAt = Time.unscaledTime + seconds;
            return;
        }
        foreach (Row p in persistent.Values) if (p.text.text == message) return;

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
        persistent.TryGetValue(key, out Row row);

        if (string.IsNullOrEmpty(message))
        {
            if (row == null) return;
            Destroy(row.go);
            persistent.Remove(key);
            return;
        }

        if (row != null && row.text.text == message) return;
        if (row != null) Destroy(row.go);

        row = CreateRow(message, kind);
        row.go.transform.SetAsFirstSibling(); // persistent problems on top
        persistent[key] = row;

        Debug.LogWarning($"[MP-NOTICE] persistent: {message}");
    }

    private Row CreateRow(string message, Kind kind)
    {
        EnsureCanvas();

        var go = new GameObject("Notice", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(stack, false);

        var background = go.GetComponent<Image>();
        background.color = kind == Kind.Error ? new Color(0.62f, 0.12f, 0.12f, 0.92f)
                         : kind == Kind.Warning ? new Color(0.70f, 0.45f, 0.08f, 0.92f)
                         : new Color(0.12f, 0.42f, 0.22f, 0.92f);
        background.raycastTarget = false;
        go.GetComponent<LayoutElement>().minHeight = 54f;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var rect = (RectTransform)textGo.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, 8f);
        rect.offsetMax = new Vector2(-18f, -8f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = message;
        text.fontSize = 24f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        return new Row { go = go, text = text, background = background };
    }

    private void EnsureCanvas()
    {
        if (stack != null) return;

        var canvasGo = new GameObject("MpNoticeCanvas", typeof(Canvas), typeof(CanvasScaler));
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
        stack.anchorMin = new Vector2(0.5f, 1f);
        stack.anchorMax = new Vector2(0.5f, 1f);
        stack.pivot = new Vector2(0.5f, 1f);
        stack.anchoredPosition = new Vector2(0f, -24f);
        stack.sizeDelta = new Vector2(1000f, 0f);

        var layout = stackGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        stackGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
