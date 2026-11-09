using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Interactions;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// Gives every interactable the same hover glow: a HighlightHelper wired to its hover-on/off events.
// Replaces the old MaterialColorChangeHelper tint and drops listeners whose target was deleted.
// Safe to re-run: objects that are already wired are left alone.
public static class HighlightSetup
{
    const string MaterialPath = "Assets/Resources/Highlight/HoverGlow.mat";
    static readonly string[] Scenes =
    {
        "Assets/Scenes/TutorialScene.unity",
        "Assets/Scenes/Level_01.unity",
        "Assets/Scenes/Level_02.unity",
        "Assets/Scenes/Level_03.unity",
        "Assets/Scenes/Level_04.unity",
        "Assets/Scenes/Level_05.unity",
    };

    [MenuItem("Tools/NailIt/Highlight/Set Up Prefabs And Levels")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("[Highlight] " + Run());
    }

    public static string Run()
    {
        EnsureMaterial();
        var log = new List<string>();
        log.Add(SetupPrefabs());
        foreach (var s in Scenes) log.Add(SetupScene(s));
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    public static void EnsureMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Highlight")) AssetDatabase.CreateFolder("Assets/Resources", "Highlight");
        var shader = Shader.Find("NailIt/HoverGlow");
        AssetDatabase.CreateAsset(new Material(shader) { name = "HoverGlow" }, MaterialPath);
    }

    public static string SetupPrefabs()
    {
        // Base prefabs first, so prefabs that nest them inherit the wiring instead of overriding it.
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => AssetDatabase.GetDependencies(p, true).Count(d => d.EndsWith(".prefab")))
            .ToList();

        var changed = new List<string>();
        var skipped = new List<string>();
        foreach (var path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int fixedCount = 0;
                foreach (var mb in Interactables(root))
                {
                    var r = Wire(mb);
                    if (r == "wired") fixedCount++;
                    else if (r != "ok") skipped.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}/{mb.name}: {r}");
                }
                fixedCount += RemoveUnusedTints(root);
                if (fixedCount > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}({fixedCount})");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return $"prefabs changed=[{string.Join(", ", changed)}] skipped=[{string.Join("; ", skipped)}]";
    }

    public static string SetupScene(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int fixedCount = 0;
        var skipped = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var mb in Interactables(root))
            {
                var r = Wire(mb);
                if (r == "wired") fixedCount++;
                else if (r != "ok") skipped.Add($"{mb.name}: {r}");
            }
            fixedCount += RemoveUnusedTints(root);
        }
        if (fixedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return $"{scene.name}: changed={fixedCount} skipped=[{string.Join("; ", skipped.Distinct())}]";
    }

    static IEnumerable<MonoBehaviour> Interactables(GameObject root) =>
        root.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m is IInteractable);

    static (UnityEvent on, UnityEvent off) HoverEvents(MonoBehaviour mb)
    {
        UnityEvent on = null, off = null;
        for (var t = mb.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.FieldType != typeof(UnityEvent)) continue;
                string n = f.Name.ToLowerInvariant();
                if (on == null && n.Contains("hoveron")) on = (UnityEvent)f.GetValue(mb);
                else if (off == null && n.Contains("hoveroff")) off = (UnityEvent)f.GetValue(mb);
            }
        }
        return (on, off);
    }

    // Returns "ok" (already wired), "wired" (changed) or why it was left alone.
    static string Wire(MonoBehaviour mb)
    {
        var (on, off) = HoverEvents(mb);
        if (on == null || off == null) return "no hover events";

        bool hasOn = Has(on, nameof(HighlightHelper.HighlightMaterial));
        bool hasOff = Has(off, nameof(HighlightHelper.ReverseHighlight));
        bool stale = Stale(on) || Stale(off);
        // An empty slot builder shows its own blueprint hover; leave it unless it used the old tint.
        if (mb is ItemScript.Constructor && on.GetPersistentEventCount() == 0) return "constructor uses blueprint hover";
        if (hasOn && hasOff && !stale) return "ok";

        Undo.RecordObject(mb, "Wire highlight");
        RemoveStale(on);
        RemoveStale(off);
        var helper = mb.GetComponent<HighlightHelper>() ?? ExistingHelper(on) ?? mb.gameObject.AddComponent<HighlightHelper>();
        if (!Has(on, nameof(HighlightHelper.HighlightMaterial))) UnityEventTools.AddVoidPersistentListener(on, helper.HighlightMaterial);
        if (!Has(off, nameof(HighlightHelper.ReverseHighlight))) UnityEventTools.AddVoidPersistentListener(off, helper.ReverseHighlight);
        EditorUtility.SetDirty(mb);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mb);
        return "wired";
    }

    static bool Has(UnityEvent e, string method)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            if (e.GetPersistentTarget(i) is HighlightHelper && e.GetPersistentMethodName(i) == method) return true;
        return false;
    }

    static HighlightHelper ExistingHelper(UnityEvent e)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            if (e.GetPersistentTarget(i) is HighlightHelper h) return h;
        return null;
    }

    // The old color tint, or a listener whose target object was deleted.
    static bool IsStale(UnityEvent e, int i) =>
        e.GetPersistentTarget(i) == null || e.GetPersistentTarget(i) is MaterialColorChangeHelper;

    static bool Stale(UnityEvent e)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++) if (IsStale(e, i)) return true;
        return false;
    }

    static void RemoveStale(UnityEvent e)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
            if (IsStale(e, i)) UnityEventTools.RemovePersistentListener(e, i);
    }

    // A tint helper nothing calls any more still makes a material copy in Awake, which blocks the warm swap.
    static int RemoveUnusedTints(GameObject root)
    {
        var used = new HashSet<Object>();
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is MaterialColorChangeHelper t && mb != t)
                    used.Add(t);
        }
        int removed = 0;
        foreach (var tint in root.GetComponentsInChildren<MaterialColorChangeHelper>(true))
        {
            if (used.Contains(tint)) continue;
            // A component that comes from a nested prefab can only be removed in that prefab.
            if (PrefabUtility.IsPartOfPrefabInstance(tint) && !PrefabUtility.IsAddedComponentOverride(tint)) continue;
            Object.DestroyImmediate(tint, true);
            removed++;
        }
        return removed;
    }
}
