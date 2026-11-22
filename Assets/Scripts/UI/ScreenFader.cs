using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fade in / fade out for every menu transition, without touching any panel:
//  * Scene change  : the new scene fades in from black (and GameSceneManager fades out to black before it loads).
//  * Settings card : opening, closing and switching its tabs is an overlay, not a transition, so it never fades.
//  * Panel change  : when a click on a Button switches panels (some objects turn off, others on), the screen goes black
//                    at once and the new panel fades in from black. (A screen snapshot cross-fade was tried first: the
//                    capture came back blank white in the editor, a "flashbang".)
// Everything runs on unscaled time, so it also works in the paused state.
public class ScreenFader : MonoBehaviour
{
    private const float SceneFadeIn = 1f;
    private const float PanelFade = 1f;
    private const string BootScene = "BootScene";

    private static ScreenFader instance;

    private Image black;
    private bool armed;
    private int signatureAtPress;
    private bool settingsOpenAtPress;
    private Coroutine fadeRoutine;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (instance != null) return;

        var go = new GameObject("ScreenFader");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScreenFader>();
    }

    // Fades the screen to black. Yield it before starting a scene load; the next scene fades back in on its own.
    public static IEnumerator FadeOut(float duration = 1f)
    {
        if (instance == null) yield break;
        yield return instance.FadeBlack(0f, 1f, duration);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        black = NewImage("Black");
        black.color = new Color(0f, 0f, 0f, 0f);
    }

    private Image NewImage(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        Stretch((RectTransform)go.transform);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || scene.name == BootScene) return;

        armed = false;
        BlackThenFadeIn(SceneFadeIn);
    }

    // Black right now, then fades away. Restarting it replaces a fade that is still running.
    private void BlackThenFadeIn(float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        black.color = Color.black;
        fadeRoutine = StartCoroutine(FadeBlack(1f, 0f, duration));
    }

    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            black.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(from, to, t / duration));
            yield return null;
        }

        black.color = new Color(0f, 0f, 0f, to);
    }

    private void Update()
    {
        if (Application.isBatchMode) return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        armed = false;
        if (!PressedOnButton(mouse.position.ReadValue())) return;

        armed = true;
        settingsOpenAtPress = SettingsOpen();
        signatureAtPress = Signature();
    }

    private void LateUpdate()
    {
        if (!armed || Application.isBatchMode) return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasReleasedThisFrame) return;

        armed = false;

        // The settings card is an overlay (opens/closes/switches tabs on the same screen): no fade.
        if (settingsOpenAtPress || SettingsOpen()) return;

        // Only when the click actually switched panels.
        if (Signature() == signatureAtPress) return;

        BlackThenFadeIn(PanelFade);
    }

    private static bool SettingsOpen()
    {
        foreach (SettingsMenu menu in FindObjectsByType<SettingsMenu>(FindObjectsSortMode.None))
            if (menu != null && menu.IsOpen) return true;
        return false;
    }

    private bool PressedOnButton(Vector2 position)
    {
        if (EventSystem.current == null) return false;

        var data = new PointerEventData(EventSystem.current) { position = position };
        hits.Clear();
        EventSystem.current.RaycastAll(data, hits);

        foreach (RaycastResult hit in hits)
        {
            if (hit.gameObject == null || hit.gameObject.transform.IsChildOf(transform)) continue;

            Button button = hit.gameObject.GetComponentInParent<Button>();
            return button != null && button.interactable;
        }

        return false;
    }

    // Which UI objects are on, a few levels below every canvas: changes when a panel is switched.
    private static int Signature()
    {
        int hash = 17;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.isRootCanvas || canvas.transform.parent != null && canvas.transform.IsChildOf(instance.transform)) continue;
            if (canvas.name == "MpNoticeCanvas") continue; // notices come and go on their own: not a panel switch
            Mix(canvas.transform, 0, ref hash);
        }

        return hash;
    }

    private static void Mix(Transform t, int depth, ref int hash)
    {
        if (depth > 5) return;

        foreach (Transform child in t)
        {
            hash = hash * 31 + (child.gameObject.activeInHierarchy ? child.GetInstanceID() : 0);
            if (child.gameObject.activeInHierarchy) Mix(child, depth + 1, ref hash);
        }
    }
}
