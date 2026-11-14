using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adds menu button sounds and the level defeat stinger when a scene loads, so no scene or
// menu prefab has to be edited. Sounds are per-client, so multiplayer menus get them too.
public static class SfxBootstrap
{
    private static SfxBank bank;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        bank = Resources.Load<SfxBank>("SfxBank");
        if (bank == null) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Hook();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Hook();

    private static void Hook()
    {
        // Multiplayer too: each machine plays only what its own player does.

        foreach (var button in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddUiHook(button.gameObject);
        foreach (var button in Object.FindObjectsByType<CustomButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddUiHook(button.gameObject);

        // LevelManager keeps its defeat event private; listen at runtime instead of editing every level.
        var field = typeof(LevelManager).GetField("onDefeatEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var manager in Object.FindObjectsByType<LevelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (field?.GetValue(manager) is UnityEvent defeat)
            {
                defeat.RemoveListener(PlayDefeat);
                defeat.AddListener(PlayDefeat);
            }
        }
    }

    private static void AddUiHook(GameObject target)
    {
        if (!target.TryGetComponent<UiSfxHook>(out _)) target.AddComponent<UiSfxHook>();
    }

    private static void PlayDefeat() => Sfx.Play2D(bank.defeat, bank.stingerVolume, 0f);

    public static void PlayHover() { if (bank != null) Sfx.Play2D(bank.buttonHover, bank.uiVolume * 0.6f, 0.04f); }
    public static void PlayClick() { if (bank != null) Sfx.Play2D(bank.buttonClick, bank.uiVolume, 0.04f); }

    // Warnings can repeat quickly, so repeats within 0.35 s are skipped.
    public static void PlayWarning() { if (bank != null) Sfx.Play(bank.warning, Vector3.zero, bank.uiVolume, 0.02f, 0f, 0.35f); }
}

