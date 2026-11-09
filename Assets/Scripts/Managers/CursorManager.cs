using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Single owner of the mouse cursor in singleplayer. The cursor is locked and hidden unless some screen
// holds it open (main menu, pause menu, emote wheel, score screen, info pop-up). Holders are counted,
// so closing one screen never hides the cursor another still needs.
// Multiplayer keeps its own cursor scripts, so this stays out of network sessions and Scenes_MP.
public static class CursorManager
{
    private static readonly HashSet<Object> holders = new();

    public static bool IsVisible => holders.Count > 0;

    public static void Show(Object holder)
    {
        if (holder != null && holders.Add(holder)) Apply();
    }

    public static void Release(Object holder)
    {
        if (holder != null && holders.Remove(holder)) Apply();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        holders.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Screens of the new scene have already registered in OnEnable; drop the ones the old scene destroyed.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        holders.RemoveWhere(h => h == null);
        Apply();
    }

    private static void Apply()
    {
        holders.RemoveWhere(h => h == null);
        if (!Manages()) return;

        bool visible = holders.Count > 0;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }

    private static bool Manages()
    {
        if (NetworkModeHelper.IsNetworked) return false;
        return !SceneManager.GetActiveScene().path.Contains("/Scenes_MP/");
    }
}
