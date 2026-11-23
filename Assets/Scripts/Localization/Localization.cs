using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public enum GameLanguage
{
    English = 0,
    Turkish = 1,
}

// The game's texts are English; Turkish is a table (Resources/Localization/Strings_tr.txt) keyed by the English text.
//   - Exact entries:   English<TAB>Turkish         (\n in the file = a line break)
//   - Template entries: "Load {0}"<TAB>"{0} yükle"  ({0}, {1} = the parts of the text that differ; each part is translated too)
// LocalizationApplier swaps the text of every TextMeshPro label that matches an entry, so scenes and prefabs stay in
// English and need no edits. Code that builds a string itself can call Localization.T(...) directly.
// The language is saved in PlayerPrefs. The process culture is NOT touched (it stays invariant on purpose: the Turkish
// culture turns the "i" of English text into a capital "İ").
public static class Localization
{
    private const string PrefKey = "NIOFI.Language";
    private const string TablePath = "Localization/Strings_tr";

    public static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");

    private static readonly Regex KeyPrefix = new Regex(@"^(\[[^\]]+\]\s*)(.+)$", RegexOptions.Singleline | RegexOptions.Compiled);

    private struct Template
    {
        public Regex pattern;
        public string result;
        public int weight; // length of the English template: the most specific one is tried first
    }

    private static Dictionary<string, string> exact;
    private static List<Template> templates;
    private static GameLanguage? current;

    public static event Action Changed;

    public static GameLanguage Current
    {
        get
        {
            if (current == null) current = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 1);
            return current.Value;
        }
    }

    public static bool IsTurkish => Current == GameLanguage.Turkish;

    public static string LanguageName(GameLanguage language) => language == GameLanguage.Turkish ? "Türkçe" : "English";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        current = null;
        exact = null;
        templates = null;
        Changed = null;
    }

    public static void Set(GameLanguage language)
    {
        if (Current == language) return;

        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // The text in the current language (the English text itself when there is no entry or the game is in English).
    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english) || !IsTurkish) return english;
        return Translate(english, 0);
    }

    private static string Translate(string text, int depth)
    {
        EnsureLoaded();

        if (exact.TryGetValue(text, out string direct)) return direct;

        // Surrounding spaces / line breaks stay where they are.
        string trimmed = text.Trim();
        if (trimmed.Length != text.Length && trimmed.Length > 0)
        {
            int start = text.IndexOf(trimmed, StringComparison.Ordinal);
            return text.Substring(0, start) + Translate(trimmed, depth + 1) + text.Substring(start + trimmed.Length);
        }

        if (depth > 3) return text;

        // "[E] Load Wood": the key hint stays, the rest is translated.
        Match key = KeyPrefix.Match(text);
        if (key.Success) return key.Groups[1].Value + Translate(key.Groups[2].Value, depth + 1);

        foreach (Template template in templates)
        {
            Match match = template.pattern.Match(text);
            if (!match.Success) continue;

            string result = template.result;
            for (int i = 1; i < match.Groups.Count; i++)
                result = result.Replace("{" + (i - 1) + "}", Translate(match.Groups[i].Value, depth + 1));

            return result;
        }

        return text;
    }

    // Turkish upper case ("i" -> "İ") for labels the font style draws in capitals.
    public static string UpperTurkish(string text) => text.ToUpper(TurkishCulture);

    private static void EnsureLoaded()
    {
        if (exact != null) return;

        exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        templates = new List<Template>();

        TextAsset asset = Resources.Load<TextAsset>(TablePath);
        if (asset == null)
        {
            Debug.LogWarning("[LOC] Resources/" + TablePath + " not found: Turkish has no texts.");
            return;
        }

        foreach (string raw in asset.text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;

            int tab = line.IndexOf('\t');
            if (tab <= 0) continue;

            string key = Unescape(line.Substring(0, tab));
            string value = Unescape(line.Substring(tab + 1));

            if (key.Contains("{0}"))
            {
                string pattern = Regex.Escape(key);
                for (int i = 0; i < 4; i++) pattern = pattern.Replace("\\{" + i + "}", "(.+?)");

                templates.Add(new Template
                {
                    pattern = new Regex("^" + pattern + "$", RegexOptions.IgnoreCase | RegexOptions.Singleline),
                    result = value,
                    weight = key.Length,
                });
            }
            else
            {
                exact[key] = value;
            }
        }

        templates.Sort((a, b) => b.weight.CompareTo(a.weight));
    }

    private static string Unescape(string value) => value.Replace("\\n", "\n").Replace("\\\\", "\\");
}
