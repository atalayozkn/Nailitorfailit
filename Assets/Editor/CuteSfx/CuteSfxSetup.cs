using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

// Builds the cute SFX set and hooks it into the game. Safe to run again: files are overwritten and
// existing SfxCue listeners are replaced, not duplicated.
public static class CuteSfxSetup
{
    const string SfxRoot = "Assets/Art/SFX/Cute";
    const string BirdRoot = "Assets/StyleTest_Warm/Audio";

    [MenuItem("Tools/NailIt/Cute SFX/Generate And Wire")]
    public static void Run()
    {
        Debug.Log(Generate());
        Debug.Log(Wire());
    }

    static string PathFor(string name, int variation, int count)
    {
        string root = name.StartsWith("Birds/") ? BirdRoot : SfxRoot;
        string file = name.Substring(name.IndexOf('/') + 1) + (count > 1 ? "_" + (variation + 1).ToString("00") : "") + ".wav";
        string folder = name.StartsWith("Birds/") ? root : root + "/" + name.Substring(0, name.IndexOf('/'));
        return folder + "/" + file;
    }

    public static string Generate()
    {
        int files = 0;
        var paths = new List<(string path, bool loop, float seconds)>();
        foreach (var recipe in CuteSfxRecipes.All)
        {
            for (int v = 0; v < recipe.Variations; v++)
            {
                var synth = recipe.Build(v);
                string path = PathFor(recipe.Name, v, recipe.Variations);
                synth.Save(path);
                paths.Add((path, recipe.Loop, synth.Data.Length / (float)SfxSynth.Rate));
                files++;
            }
        }
        AssetDatabase.Refresh();
        foreach (var (path, loop, seconds) in paths)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = loop || seconds > 2f ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        return $"[CuteSfx] generated {files} files";
    }

    // ---------- Wiring ----------

    static AudioClip[] Clips(string name)
    {
        string folder = Path.GetDirectoryName(PathFor(name, 0, 1)).Replace('\\', '/');
        string stem = name.Substring(name.IndexOf('/') + 1);
        return AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => { string f = Path.GetFileNameWithoutExtension(p); return f == stem || (f.StartsWith(stem + "_") && int.TryParse(f.Substring(stem.Length + 1), out _)); })
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();
    }

    public static string Wire()
    {
        var log = new List<string>();
        log.Add(WireLibrary());
        log.Add(WireBank());
        log.Add(WirePrefabs());
        log.Add(WireBirds());
        AssetDatabase.SaveAssets();
        return "[CuteSfx] " + string.Join(" | ", log);
    }

    static string WireLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>("Assets/Resources/VfxLibrary.asset");
        if (library == null) return "VfxLibrary missing";
        var map = new Dictionary<VfxId, (string clip, float volume, float spatial)>
        {
            { VfxId.TilePlaced, ("Construction/TilePlace", 0.75f, 0.6f) },
            { VfxId.TileNail, ("Construction/Nail", 0.7f, 0.6f) },
            { VfxId.TileFinished, ("Construction/TileFinished", 0.7f, 0.3f) },
            { VfxId.RoomFinished, ("Construction/RoomFinished", 0.85f, 0f) },
            { VfxId.CutterCutting, ("Stations/CutterCut", 0.6f, 0.6f) },
            { VfxId.ProductOut, ("Stations/ProductOut", 0.7f, 0.6f) },
            { VfxId.MixerDone, ("Stations/MixerDone", 0.75f, 0.5f) },
            { VfxId.MixerFail, ("Stations/MixerFail", 0.75f, 0.5f) },
            { VfxId.Refuel, ("Stations/Refuel", 0.7f, 0.6f) },
            { VfxId.OutOfPower, ("Stations/OutOfPower", 0.75f, 0.5f) },
            { VfxId.FireIgnite, ("Fire/Ignite", 0.7f, 0.7f) },
            { VfxId.FireBurn, ("Fire/BurnLoop", 0.45f, 0.85f) },
            { VfxId.FireOut, ("Fire/Out", 0.7f, 0.6f) },
            { VfxId.TileDestroyedWood, ("TileDamage/DestroyedWood", 0.8f, 0.6f) },
            { VfxId.TileDestroyedBrick, ("TileDamage/DestroyedBrick", 0.8f, 0.6f) },
            { VfxId.TileDestroyedGlass, ("TileDamage/DestroyedGlass", 0.8f, 0.6f) },
            { VfxId.PipeBurst, ("Hazards/PipeBurst", 0.75f, 0.7f) },
            { VfxId.SocketSpark, ("Hazards/SocketSpark", 0.7f, 0.7f) },
            { VfxId.TileElectrified, ("Hazards/ElectrifiedLoop", 0.4f, 0.85f) },
            { VfxId.PlayerElectrocuted, ("Hazards/PlayerElectrocuted", 0.8f, 0.2f) },
            { VfxId.PlayerDrained, ("Player/OutOfStamina", 0.7f, 0.2f) },
            { VfxId.DogPet, ("Dog/Petted", 0.7f, 0.5f) },
            { VfxId.DogGift, ("Dog/Gift", 0.7f, 0.5f) },
            { VfxId.DogRebel, ("Dog/Rebel", 0.7f, 0.5f) },
            { VfxId.DogMoodWorse, ("Dog/MoodWorse", 0.6f, 0.5f) },
            { VfxId.BowlFill, ("Dog/BowlFill", 0.7f, 0.6f) },
            { VfxId.MailCollect, ("Mail/Collect", 0.75f, 0.3f) },
            { VfxId.Victory, ("UI/Victory", 0.85f, 0f) },
        };
        int done = 0;
        foreach (var pair in map)
        {
            var entry = library.entries.FirstOrDefault(e => e.id == pair.Key);
            if (entry == null)
            {
                entry = new VfxLibrary.Entry { id = pair.Key };
                library.entries.Add(entry);
            }
            entry.sounds = Clips(pair.Value.clip);
            entry.volume = pair.Value.volume;
            entry.spatial = pair.Value.spatial;
            entry.pitchJitter = pair.Value.clip.Contains("Loop") || pair.Value.spatial == 0f ? 0f : 0.06f;
            if (entry.sounds.Length > 0) done++;
        }
        EditorUtility.SetDirty(library);
        return $"library {done}/{map.Count}";
    }

    static string WireBank()
    {
        const string path = "Assets/Resources/SfxBank.asset";
        var bank = AssetDatabase.LoadAssetAtPath<SfxBank>(path);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SfxBank>();
            AssetDatabase.CreateAsset(bank, path);
        }
        bank.buttonHover = Clips("UI/Hover");
        bank.buttonClick = Clips("UI/Click");
        bank.defeat = Clips("UI/Defeat");
        bank.warning = Clips("UI/Warning");
        EditorUtility.SetDirty(bank);
        return "bank";
    }

    static UnityEvent GetEvent(Component component, string field)
    {
        var info = component.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return info?.GetValue(component) as UnityEvent;
    }

    // Adds (or refreshes) an SfxCue next to the component and points its event at it.
    static bool Hook(Component component, string eventField, string clipName, float volume, float spatial, float minInterval, string method = "Play")
    {
        var evt = GetEvent(component, eventField);
        if (evt == null) return false;
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
            if (evt.GetPersistentTarget(i) is SfxCue) UnityEventTools.RemovePersistentListener(evt, i);

        var cue = component.GetComponents<SfxCue>().FirstOrDefault() ?? component.gameObject.AddComponent<SfxCue>();
        cue.Setup(Clips(clipName), volume, 0.06f, spatial, minInterval);
        var action = method == "Play" ? new UnityAction(cue.Play) : new UnityAction(cue.PlayLoop);
        UnityEventTools.AddPersistentListener(evt, action);
        EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(cue);
        return true;
    }

    static int EditPrefabs<T>(System.Func<T, string, bool> edit) where T : Component
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<T>(true) == null) continue;

            // Only touch components the prefab owns, not ones it inherits from a nested prefab.
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var component in root.GetComponentsInChildren<T>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(component) && !PrefabUtility.IsAddedComponentOverride(component)) continue;
                changed |= edit(component, path);
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        return count;
    }

    static string WirePrefabs()
    {
        int spawners = EditPrefabs<ObjectSpawner>((c, path) =>
        {
            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            string clip = file.Contains("brick") ? "Construction/TakeBrick" : file.Contains("cement") ? "Construction/TakeCement" : file.Contains("glass") ? "Construction/TakeGlass" : "Construction/TakeWood";
            return Hook(c, "onInteractEvent", clip, 0.7f, 0.6f, 0.08f);
        });
        int carriables = EditPrefabs<ItemScript.CarriableObject_SP>((c, path) => Hook(c, "onInteractEvent", "Construction/PickUp", 0.6f, 0.6f, 0.08f));
        int tiles = EditPrefabs<ObjectHealth>((c, path) =>
        {
            string file = Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith("FloorTile_")) return false;
            string clip = file.Contains("Brick") ? "TileDamage/HitBrick" : file.Contains("Glass") ? "TileDamage/HitGlass" : "TileDamage/HitWood";
            return Hook(c, "onHitEvent", clip, 0.45f, 0.7f, 0.35f);
        });
        int mail = EditPrefabs<MailBox>((c, path) => Hook(c, "onTriggerEvent", "Mail/Arrived", 0.75f, 0.3f, 0.5f));
        int horns = EditPrefabs<TrapVehicle>((c, path) => Hook(c, "onTrapTriggeredEvent", "Traffic/Horn", 0.7f, 0.3f, 1f));
        int cars = EditPrefabs<Car_Active>((c, path) =>
        {
            var source = c.GetComponents<AudioSource>().FirstOrDefault(s => s.clip != null && s.clip.name == "CarEngineLoop") ?? c.gameObject.AddComponent<AudioSource>();
            source.clip = Clips("Traffic/CarEngineLoop").FirstOrDefault();
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.35f;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            var mixer = Resources.Load<AudioMixer>("Audio/GameAudioMixer");
            source.outputAudioMixerGroup = mixer != null ? mixer.FindMatchingGroups("Sfx").FirstOrDefault() : null;
            EditorUtility.SetDirty(source);
            return true;
        });
        return $"prefabs spawners={spawners} carriables={carriables} tiles={tiles} mail={mail} horn={horns} cars={cars}";
    }

    // The style test ambience prefers these files over its code-made chirps.
    static string WireBirds()
    {
        var ambience = Object.FindFirstObjectByType<StyleTestAmbience>();
        if (ambience == null) return "birds: open the style test scene to wire";
        var field = typeof(StyleTestAmbience).GetField("chirpClips", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(ambience, Clips("Birds/Chirp").ToList());
        EditorUtility.SetDirty(ambience);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ambience.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(ambience.gameObject.scene);
        return "birds wired";
    }
}
