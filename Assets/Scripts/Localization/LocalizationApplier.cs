using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Swaps the text of every TextMeshPro label to the current language, whatever created it (scene, prefab or code).
// A label is looked at when its text changes (TextMeshPro's own event) and for the whole scene when a scene loads or the
// language changes. The English original of every label it changed is remembered, so going back to English restores it.
[DefaultExecutionOrder(32000)]
public class LocalizationApplier : MonoBehaviour
{
    private sealed class Entry
    {
        public string english;
        public string shown;
        public bool setAgain; // the owner of the label writes the same English text again and again
    }

    private readonly Dictionary<TMP_Text, Entry> entries = new Dictionary<TMP_Text, Entry>();
    private bool applying;
    private float nextCleanupAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Localization.Enabled) return;
        if (FindAnyObjectByType<LocalizationApplier>() != null) return;

        var go = new GameObject("LocalizationApplier");
        DontDestroyOnLoad(go);
        go.AddComponent<LocalizationApplier>();
    }

    private void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Localization.Changed += SweepAll;
        SweepAll();
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Localization.Changed -= SweepAll;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCleanupAt) return;
        nextCleanupAt = Time.unscaledTime + 30f;

        var dead = new List<TMP_Text>();
        foreach (TMP_Text label in entries.Keys)
            if (label == null) dead.Add(label);

        foreach (TMP_Text label in dead) entries.Remove(label);
    }

    // Labels whose owner keeps writing the English text (every refresh) are put back to Turkish before they are drawn.
    private void LateUpdate()
    {
        if (!Localization.IsTurkish) return;

        foreach (KeyValuePair<TMP_Text, Entry> pair in entries)
        {
            if (!pair.Value.setAgain || pair.Key == null) continue;
            if (pair.Key.text != pair.Value.shown) SetText(pair.Key, pair.Value.shown);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Localization.IsTurkish) SweepAll();
    }

    private void OnTextChanged(Object changed)
    {
        if (applying || !(changed is TMP_Text label)) return;
        Apply(label);
    }

    private void SweepAll()
    {
        // Back to the English originals first, then translate again if the language needs it.
        foreach (KeyValuePair<TMP_Text, Entry> pair in entries)
        {
            if (pair.Key != null && pair.Key.text == pair.Value.shown) SetText(pair.Key, pair.Value.english);
        }

        entries.Clear();

        if (!Localization.IsTurkish) return;

        foreach (TMP_Text label in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Apply(label);
    }

    private void Apply(TMP_Text label)
    {
        if (label == null) return;

        string current = label.text;
        if (string.IsNullOrEmpty(current)) return;

        if (entries.TryGetValue(label, out Entry entry))
        {
            if (current == entry.shown) return; // our own change coming back as an event

            if (current == entry.english && Localization.IsTurkish)
            {
                entry.setAgain = true;           // the same English text written again: keep it in Turkish
                SetText(label, entry.shown);
                return;
            }

            entries.Remove(label);               // somebody set a new text
        }

        if (!Localization.IsTurkish) return;

        string translated = Localization.T(current);
        if (translated == current) return;

        // Labels drawn in capitals get Turkish capitals ("i" -> "İ"); the English path is untouched.
        if ((label.fontStyle & FontStyles.UpperCase) != 0) translated = Localization.UpperTurkish(translated);

        entries[label] = new Entry { english = current, shown = translated };
        SetText(label, translated);
    }

    private void SetText(TMP_Text label, string value)
    {
        applying = true;
        label.text = value;
        applying = false;
    }
}
