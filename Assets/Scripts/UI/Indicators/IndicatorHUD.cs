using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws every shown WorldIndicator as a screen badge: above the object when it is on screen, pinned to the screen
// edge with an arrow pointing at it when it is off screen or behind the camera. Created on demand in singleplayer;
// multiplayer never shows it. Draws over the HUD but under menus and pop-ups, and hides while paused.
public class IndicatorHUD : MonoBehaviour
{
    private const float BadgeSize = 58f;
    private const float EdgeMargin = 70f;
    private const float FadeRange = 2f;

    private class Badge
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image disc, ring, iconImage, arrow;
        public TextMeshProUGUI label;
    }

    private static IndicatorHUD instance;
    private static Sprite circleSprite, arrowSprite;

    private readonly Dictionary<string, Badge> badges = new();
    private readonly Dictionary<string, (Vector3 sum, int count, WorldIndicator first)> groups = new();
    private readonly HashSet<string> used = new();
    private RectTransform canvasRect;
    private Camera cam;
    private Transform player;

    public static void EnsureExists()
    {
        if (instance != null || NetworkModeHelper.IsNetworked || !Application.isPlaying) return;
        var go = new GameObject("IndicatorHUD");
        instance = go.AddComponent<IndicatorHUD>();
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // over the stamina bar and HUD (0), under Settings (400) and warnings/pop-ups (500)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)transform;
        if (circleSprite == null) BuildSprites();
    }
    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (player == null)
        {
            var handler = FindFirstObjectByType<PlayerInteractionHandler>();
            if (handler != null) player = handler.transform;
        }

        // Merge grouped indicators into one point each.
        groups.Clear();
        foreach (var indicator in WorldIndicator.Active)
        {
            if (!indicator.IsShown) continue;
            string key = string.IsNullOrEmpty(indicator.GroupKey) ? "#" + indicator.GetInstanceID() : indicator.GroupKey;
            groups.TryGetValue(key, out var g);
            groups[key] = (g.sum + indicator.WorldPoint, g.count + 1, g.first != null ? g.first : indicator);
        }

        used.Clear();
        // Hidden while paused, so the pause and settings screens stay clean.
        if (cam != null && Time.timeScale > 0f)
        {
            foreach (var pair in groups)
            {
                used.Add(pair.Key);
                Place(GetBadge(pair.Key, pair.Value.first), pair.Value.first, pair.Value.sum / pair.Value.count);
            }
        }
        foreach (var pair in badges)
            if (!used.Contains(pair.Key)) pair.Value.root.gameObject.SetActive(false);
    }

    private void Place(Badge badge, WorldIndicator source, Vector3 world)
    {
        badge.root.gameObject.SetActive(true);

        float alpha = 1f;
        if (source.HideWithinDistance > 0f && player != null)
        {
            Vector3 flat = world - player.position; flat.y = 0f;
            alpha = Mathf.InverseLerp(source.HideWithinDistance, source.HideWithinDistance + FadeRange, flat.magnitude);
        }
        badge.group.alpha = alpha;

        Vector3 screen = cam.WorldToScreenPoint(world);
        bool behind = screen.z < 0f;
        if (behind) screen = new Vector3(Screen.width - screen.x, Screen.height - screen.y, 0f); // mirror so the arrow points the right way

        float scale = canvasRect.localScale.x;
        float margin = EdgeMargin * scale;
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        bool onScreen = !behind && screen.x > margin && screen.x < Screen.width - margin && screen.y > margin && screen.y < Screen.height - margin;

        Vector2 point = screen;
        badge.arrow.enabled = !onScreen;
        if (!onScreen)
        {
            Vector2 dir = (Vector2)screen - center;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            float tx = Mathf.Abs(dir.x) > 0.001f ? (center.x - margin) / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.001f ? (center.y - margin) / Mathf.Abs(dir.y) : float.MaxValue;
            point = center + dir * Mathf.Min(tx, ty);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            badge.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            badge.arrow.rectTransform.anchoredPosition = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right) * (BadgeSize * 0.62f);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, null, out Vector2 local);
        badge.root.anchoredPosition = local;
        float pulse = source.Pulse ? 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 7f) : 1f;
        badge.root.localScale = Vector3.one * pulse;
    }

    private Badge GetBadge(string key, WorldIndicator source)
    {
        if (badges.TryGetValue(key, out var badge)) return badge;

        var root = new GameObject("Badge_" + key, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.sizeDelta = new Vector2(BadgeSize, BadgeSize);
        badge = new Badge { root = root, group = root.GetComponent<CanvasGroup>() };
        badge.group.blocksRaycasts = false;
        badge.group.interactable = false;

        badge.arrow = MakeImage("Arrow", root, arrowSprite, BadgeSize * 0.42f, Darken(source.Color, 0.75f));
        badge.ring = MakeImage("Ring", root, circleSprite, BadgeSize, Darken(source.Color, 0.55f));
        badge.disc = MakeImage("Disc", root, circleSprite, BadgeSize * 0.84f, source.Color);
        if (source.Icon != null)
        {
            badge.iconImage = MakeImage("Icon", root, source.Icon, BadgeSize * 0.6f, Color.white);
            badge.iconImage.preserveAspect = true;
        }
        else
        {
            var labelGo = new GameObject("Glyph", typeof(RectTransform));
            labelGo.transform.SetParent(root, false);
            badge.label = labelGo.AddComponent<TextMeshProUGUI>();
            badge.label.text = source.Glyph;
            badge.label.fontSize = BadgeSize * 0.6f;
            badge.label.fontStyle = FontStyles.Bold;
            badge.label.alignment = TextAlignmentOptions.Center;
            badge.label.color = Color.white;
            badge.label.raycastTarget = false;
            var r = badge.label.rectTransform;
            r.sizeDelta = new Vector2(BadgeSize, BadgeSize);
        }
        badges[key] = badge;
        return badge;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        return image;
    }
    private static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

    // Procedural sprites so the system works without art; WorldIndicator.icon replaces the glyph with a real icon.
    private static void BuildSprites()
    {
        circleSprite = MakeSprite(128, (x, y, s) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s, s) * 0.5f);
            return Mathf.Clamp01(s * 0.5f - d);
        });
        arrowSprite = MakeSprite(64, (x, y, s) =>
        {
            // Triangle pointing up: base at the bottom, tip at the top centre.
            float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
            float halfWidth = 0.5f * (1f - v);
            float edge = (halfWidth - Mathf.Abs(u - 0.5f)) * s;
            return Mathf.Clamp01(Mathf.Min(edge, v * s));
        });
    }
    private static Sprite MakeSprite(int size, System.Func<int, int, int, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha(x, y, size) * 255f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
