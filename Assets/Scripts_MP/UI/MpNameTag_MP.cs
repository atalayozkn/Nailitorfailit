using PlayerScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Name above every player's head in a match: the player's lobby colour, a mic icon (open / talking / muted) in front.
// A world-space canvas under the player's body that turns to face the camera. Built in code, one per player copy.
public class MpNameTag_MP : MonoBehaviour
{
    private const float HeightAboveBody = 2.35f;
    private const float CanvasScale = 0.0062f; // canvas pixels -> metres
    private const float IconSize = 54f;
    private const float Gap = 10f;
    private const float FontSize = 52f;

    private PlayerNetwork_MP player;
    private VoiceSpeaker_MP speaker;
    private Transform canvasRoot;
    private TextMeshProUGUI label;
    private Image icon;
    private Image crownIcon;
    private Camera cam;

    private string shownName;
    private int shownColor = int.MinValue;
    private bool shownHost;

    private static readonly Color CrownColor = new Color(1f, 0.82f, 0.25f, 1f);
    private static readonly Color MicOpenColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    private static readonly Color MicMutedColor = new Color(0.92f, 0.35f, 0.35f, 1f);
    private static readonly Color MicTalkingColor = new Color(0.40f, 0.85f, 0.45f, 1f);

    public static void Attach(PlayerNetwork_MP player)
    {
        if (player == null || player.GetComponentInChildren<MpNameTag_MP>(true) != null) return;

        var host = new GameObject("NameTag");
        host.transform.SetParent(player.transform, false);
        host.transform.localPosition = new Vector3(0f, HeightAboveBody, 0f);
        host.AddComponent<MpNameTag_MP>().Build(player);
    }

    private void Build(PlayerNetwork_MP owner)
    {
        player = owner;

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvasRoot = canvasGo.transform;

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = new Vector2(520f, 90f);
        canvasRect.localScale = Vector3.one * CanvasScale;

        var iconGo = new GameObject("Mic", typeof(RectTransform));
        iconGo.transform.SetParent(canvasRoot, false);
        ((RectTransform)iconGo.transform).sizeDelta = new Vector2(IconSize, IconSize);
        icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        var crownGo = new GameObject("Crown", typeof(RectTransform));
        crownGo.transform.SetParent(canvasRoot, false);
        ((RectTransform)crownGo.transform).sizeDelta = new Vector2(IconSize, IconSize);
        crownIcon = crownGo.AddComponent<Image>();
        crownIcon.sprite = MpMicIcon_MP.Crown;
        crownIcon.color = CrownColor;
        crownIcon.raycastTarget = false;
        crownIcon.preserveAspect = true;

        var labelGo = new GameObject("Name", typeof(RectTransform));
        labelGo.transform.SetParent(canvasRoot, false);
        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.sizeDelta = new Vector2(420f, 72f);
        label = labelGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = FontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.outlineWidth = 0.28f;
        label.outlineColor = new Color32(0, 0, 0, 230);
    }

    private void LateUpdate()
    {
        if (player == null) { Destroy(gameObject); return; }

        bool visible = !player.isDead;
        if (canvasRoot.gameObject.activeSelf != visible) canvasRoot.gameObject.SetActive(visible);
        if (!visible) return;

        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam != null) canvasRoot.rotation = cam.transform.rotation;

        RefreshText();
        RefreshMic();
    }

    private void RefreshText()
    {
        string displayName = string.IsNullOrEmpty(player.playerName) ? "Player" : player.playerName;
        int color = player.colorIndex;
        bool host = player.isHostPlayer;

        if (displayName == shownName && color == shownColor && host == shownHost) return;

        shownName = displayName;
        shownColor = color;
        shownHost = host;

        label.text = displayName;
        label.color = color >= 0 ? MpPlayerColor_MP.ColorOf(color) : Color.white;
        label.ForceMeshUpdate();

        // <name> <crown if host> <mic>, the whole row centred above the head.
        float textWidth = Mathf.Min(label.preferredWidth, 420f);
        float total = textWidth + (host ? Gap + IconSize : 0f) + Gap + IconSize;
        float x = -total * 0.5f;

        RectTransform labelRect = label.rectTransform;
        labelRect.sizeDelta = new Vector2(textWidth + 4f, 72f);
        labelRect.anchoredPosition = new Vector2(x + textWidth * 0.5f, 0f);
        x += textWidth;

        crownIcon.gameObject.SetActive(host);
        if (host)
        {
            x += Gap;
            ((RectTransform)crownIcon.transform).anchoredPosition = new Vector2(x + IconSize * 0.5f, 0f);
            x += IconSize;
        }

        x += Gap;
        ((RectTransform)icon.transform).anchoredPosition = new Vector2(x + IconSize * 0.5f, 0f);
    }

    private void RefreshMic()
    {
        bool muted = player.micMuted;
        bool talking = !muted && IsTalking();

        icon.sprite = MpMicIcon_MP.For(muted);
        icon.color = muted ? MicMutedColor : talking ? MicTalkingColor : MicOpenColor;
    }

    private bool IsTalking()
    {
        if (player.isOwned) return VoiceNetwork_MP.Instance != null && VoiceNetwork_MP.Instance.IsTransmitting;

        if (speaker == null)
        {
            NetworkIdentityHolder();
        }

        return speaker != null && speaker.IsSpeaking;
    }

    // The voice player is added to the player's NetworkIdentity object the first time its packets arrive.
    private void NetworkIdentityHolder()
    {
        Mirror.NetworkIdentity identity = player.GetComponentInParent<Mirror.NetworkIdentity>();
        if (identity != null) speaker = identity.GetComponent<VoiceSpeaker_MP>();
    }
}
