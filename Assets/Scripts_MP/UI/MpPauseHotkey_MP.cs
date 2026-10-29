using System.Reflection;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class MpPauseHotkey_MP : MonoBehaviour
{
    private static readonly FieldInfo PauseButtonField =
        typeof(PauseMenu).GetField("pauseButton", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo SetPauseStateMethod =
        typeof(PauseMenu).GetMethod("SetPauseState", BindingFlags.Instance | BindingFlags.NonPublic);

    private float lastToggleTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpPauseHotkey_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpPauseHotkey_MP").AddComponent<MpPauseHotkey_MP>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!NetworkServer.active && !NetworkClient.active) return;

        PauseMenu menu = LocalPauseMenu();
        if (menu == null) return;

        DisableSpBinding(menu);

        if (ChatUI.IsAnyOpen || ChatUI.EscapeHandledRecently) return;

        if (!EscapePressed()) return;
        if (Time.unscaledTime - lastToggleTime < 0.2f) return;

        lastToggleTime = Time.unscaledTime;
        Toggle(menu);
    }

    private static void DisableSpBinding(PauseMenu menu)
    {
        if (PauseButtonField == null) return;

        InputActionReference reference = PauseButtonField.GetValue(menu) as InputActionReference;
        if (reference == null || reference.action == null) return;
        if (!reference.action.enabled) return;

        reference.action.Disable();
    }

    private void Toggle(PauseMenu menu)
    {
        if (GamePauseManager.Instance == null)
        {
            Debug.LogWarning("[MP-PAUSE] GamePauseManager yok, ESC yok sayildi.");
            return;
        }

        bool paused = GamePauseManager.Instance.IsPaused;

        if (paused)
        {
            menu.Resume();
        }
        else if (SetPauseStateMethod != null)
        {
            SetPauseStateMethod.Invoke(menu, new object[] { true });
        }

        bool nowPaused = GamePauseManager.Instance.IsPaused;

        Cursor.lockState = nowPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = nowPaused;

        Debug.Log($"[MP-PAUSE] ESC -> {(nowPaused ? "paused" : "resumed")}");
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
        return false;
    }

    private static PauseMenu LocalPauseMenu()
    {
        if (NetworkClient.localPlayer == null) return null;

        return NetworkClient.localPlayer.GetComponentInChildren<PauseMenu>(true);
    }
}
