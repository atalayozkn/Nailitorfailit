using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MpHudAdapter_MP : MonoBehaviour
{
    private static MpHudAdapter_MP instance;

    private Vector2Int lastScreen;
    private int lastPlayerId;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpHudAdapter_MP>(FindObjectsInactive.Include) != null) return;

        var host = new GameObject("MpHudAdapter_MP");
        DontDestroyOnLoad(host);
        host.AddComponent<MpHudAdapter_MP>();
    }

    private void Awake()
    {
        instance = this;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;

        if (!NetworkClient.active && !NetworkServer.active) return;

        NetworkIdentity local = NetworkClient.localPlayer;
        int playerId = local != null ? local.GetInstanceID() : 0;
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);

        if (playerId == lastPlayerId && screen == lastScreen) return;

        lastPlayerId = playerId;
        lastScreen = screen;

        Adapt(local);
    }

    private void Adapt(NetworkIdentity local)
    {
        int scalers = 0;

        if (local != null)
            scalers += AdaptHierarchy(local.transform);

        if (MpMenuUI.Instance != null)
            scalers += AdaptHierarchy(MpMenuUI.Instance.transform);

        if (MpAudioListenerGuard.Instance != null)
            scalers += 0;

        if (scalers > 0)
            InteractionDebug.Log($"[MP-HUD] {scalers} canvas scaler(s) adapted to {Screen.width}x{Screen.height}.");
    }

    private static int AdaptHierarchy(Transform root)
    {
        if (root == null) return 0;

        int count = 0;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        foreach (CanvasScaler scaler in root.GetComponentsInChildren<CanvasScaler>(true))
        {
            if (scaler == null) continue;

            bool changed = false;

            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                changed = true;
            }

            if (scaler.referenceResolution != new Vector2(1920f, 1080f))
            {
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                changed = true;
            }

            if (!Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
            {
                scaler.matchWidthOrHeight = 0.5f;
                changed = true;
            }

            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
            {
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                changed = true;
            }

            if (changed) count++;
        }

        if (font == null) return count;

        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null || label.font == font) continue;

            label.font = font;
            label.fontSharedMaterial = font.material;
        }

        return count;
    }
}
