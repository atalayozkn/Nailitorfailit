using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MpEndUiSuppressor_MP
{
    private static readonly string[] EndWords = { "VICTORY", "DEFEAT" };

    public static void Suppress(Scene scene)
    {
        if (!scene.IsValid()) return;

        int cleared = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
            cleared += Strip(root);

        InteractionDebug.Log($"[MP-ENDUI] '{scene.name}': {cleared} VICTORY/DEFEAT label(s) blanked " +
                             "(panels, banners and end-of-round events left untouched).");
    }

    public static int StripIn(GameObject root) => Strip(root);

    private static int Strip(GameObject root)
    {
        if (root == null) return 0;

        int cleared = 0;

        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null || string.IsNullOrEmpty(label.text)) continue;
            if (!HoldsEndWord(label.text)) continue;

            label.text = string.Empty;
            cleared++;
        }

        foreach (Text label in root.GetComponentsInChildren<Text>(true))
        {
            if (label == null || string.IsNullOrEmpty(label.text)) continue;
            if (!HoldsEndWord(label.text)) continue;

            label.text = string.Empty;
            cleared++;
        }

        return cleared;
    }

    private static bool HoldsEndWord(string value)
    {
        string normalized = value.Trim().ToUpperInvariant();

        foreach (string word in EndWords)
        {
            if (normalized.Contains(word)) return true;
        }

        return false;
    }
}
