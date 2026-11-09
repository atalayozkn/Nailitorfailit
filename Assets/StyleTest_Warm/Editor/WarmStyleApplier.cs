using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Applies the warm style test's look to the real levels and the tutorial.
// Included: painted materials, foliage swap, lantern lamps, sun/sky/fog, post volume, cloud shadows,
// butterflies/birds/chirps/pollen, a pond and planters/crates placed on free ground near each house.
// Left out on purpose: terrain repaint, terrain trees, flower scatter (they land on roads and sidewalks
// because every level has its own layout), the extra scene dog and the camera framing.
// Safe to re-run: it removes what it added last time first. Shared prefabs and materials are never edited.
public static partial class StyleTestBuilder
{
    public static readonly string[] WarmScenes =
    {
        "Assets/Scenes/TutorialScene.unity",
        "Assets/Scenes/Level_01.unity",
        "Assets/Scenes/Level_02.unity",
        "Assets/Scenes/Level_03.unity",
        "Assets/Scenes/Level_04.unity",
        "Assets/Scenes/Level_05.unity",
    };

    const string LevelGen = Gen + "/Levels";
    const string WarmRootName = "WarmStyle";
    static readonly string[] AddedNames = { "StyleTest_LampPost", "StyleTest_Pollen", "StyleTest_Volume", "StyleTest_Runtime", "StyleTest_Pond", "StyleTest_Dressing", "StyleTest_Ambience" };

    [MenuItem("Tools/NailIt/Warm Style/Apply To Open Scene")]
    static void ApplyToOpenSceneMenu()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("[WarmStyle] " + ApplyToScene(scene.path));
    }

    [MenuItem("Tools/NailIt/Warm Style/Apply To Levels And Tutorial")]
    static void ApplyToAllMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var path in WarmScenes) Debug.Log("[WarmStyle] " + ApplyToScene(path));
    }

    struct Anchors
    {
        public Vector3 house, utility, spawn;
        public bool hasUtility;
    }

    public static string ApplyToScene(string scenePath, System.Action<string> afterOpen = null)
    {
        materialSetup.Clear();
        EnsureFolder(Root, "Generated");
        EnsureFolder(Gen, "Textures");
        EnsureFolder(Gen, "Materials");
        EnsureFolder(Gen, "Levels");

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        afterOpen?.Invoke(scene.name);
        RemoveWarm();

        string holderPath = LevelGen + "/" + scene.name + "_Meshes.asset";
        AssetDatabase.DeleteAsset(holderPath);
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FoliageMeshHolder>(), holderPath);
        meshHolderOverride = holderPath;
        try
        {
            var noise = Tex("PaintNoise") ?? BuildNoise();
            var surface = Shader.Find("NailIt/WarmStylized");
            var dither = Shader.Find("Hidden/OcclusionDither/WarmStylized");
            if (surface == null || dither == null) return scene.name + ": shaders not found";

            var map = BuildMaterialMap(surface, noise);
            int swapped = ApplyMaterials(map);
            int foliage = ApplyFoliage(surface, noise);
            var foliageMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Foliage.mat");
            ApplyLampGlow(surface, noise);
            string treeLog = PaintSceneTrees(foliageMat);
            Physics.SyncTransforms();

            var anchors = FindAnchors();
            HutCenter = anchors.house;
            rejects.Clear();
            var blockers = VisualBlockers();
            Physics.SyncTransforms();

            // The tutorial's small layout leaves no clean spot for them, so it gets no pond or planters.
            bool props = scene.name != "TutorialScene";
            string pondLog = props ? "pond=noSpace" : "pond=skipped";
            var pondSpots = props ? FreeSpots(anchors, anchors.house, 9f, 34f, new Vector3(PondRadius + 0.9f, 1f, PondRadius + 0.9f), PondRadius + 3f, blockers) : new();
            if (pondSpots.Count > 0)
            {
                pondCenter = pondSpots[0].pos;
                pondLog = AddPond(surface, noise, foliageMat);
            }
            int dressed = props ? AddLevelDressing(foliageMat, anchors, blockers) : 0;

            if (Tex("CloudShadows") == null) BuildCloudTexture();
            ApplyLighting();
            ApplyVolume();
            var sun = GameObject.Find("Directional Light_Real").GetComponent<Light>();
            AddAmbience(sun);
            AddMotes();

            var host = new GameObject("StyleTest_Runtime");
            var runtime = host.AddComponent<StyleTestRuntime>();
            int missing = RelinkTextures(map);
            ReapplyMaterialSetup();
            ApplyCloudCookie(sun);
            runtime.SetData(map.Keys.ToList(), map.Values.ToList(), surface, dither);
            EditorUtility.SetDirty(runtime);

            // Everything added at scene level goes under one root, so it is easy to find or delete.
            var root = new GameObject(WarmRootName);
            foreach (var name in new[] { "StyleTest_Volume", "StyleTest_Runtime", "StyleTest_Pond", "StyleTest_Dressing", "StyleTest_Ambience" })
            {
                var go = GameObject.Find(name);
                if (go != null && go.transform.parent == null) go.transform.SetParent(root.transform, true);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"{scene.name}: materials={map.Count} slotsSwapped={swapped} foliage={foliage} {treeLog} {pondLog} dressing={dressed} missingTextures={missing} house={anchors.house} spawn={anchors.spawn} rejects[{RejectLog()}]";
        }
        finally
        {
            meshHolderOverride = null;
            pondCenter = null;
            HutCenter = DefaultHutCenter;
        }
    }

    // Undo a previous run: added objects go, hidden lamps come back. Material swaps are kept (they re-map to the same assets).
    static void RemoveWarm()
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == WarmRootName) Object.DestroyImmediate(go);

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && AddedNames.Contains(t.name)) Object.DestroyImmediate(t.gameObject);

        foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (f.sharedMesh == null || !AssetDatabase.GetAssetPath(f.sharedMesh).Contains("sokak lambası")) continue;
            var r = f.GetComponent<MeshRenderer>();
            if (r != null && !r.enabled)
            {
                r.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }
    }

    static Anchors FindAnchors()
    {
        var a = new Anchors();
        var cm = Object.FindFirstObjectByType<CinemachineCamera>();
        a.spawn = cm != null && cm.Follow != null ? cm.Follow.position : Vector3.zero;

        var house = GameObject.Find("HouseCenter");
        if (house != null) a.house = house.transform.position;
        else
        {
            // The tutorial has no marker; its build site (the dog house) is the yard's centre.
            var room = Object.FindFirstObjectByType<RoomController>(FindObjectsInactive.Include);
            a.house = room != null ? room.transform.position : a.spawn;
        }

        var utility = GameObject.Find("UtilityCenter");
        a.hasUtility = utility != null;
        a.utility = a.hasUtility ? utility.transform.position : a.house;
        return a;
    }

    // Bounds of visible geometry that may have no collider: flat pieces (roads, sidewalks, kerbs) and small props.
    // Tall merged meshes such as a whole fence line are skipped, since their box would cover the yard they enclose;
    // their colliders still guard them.
    static List<Bounds> VisualBlockers()
    {
        var list = new List<Bounds>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled) continue;
            var b = r.bounds;
            float area = b.size.x * b.size.z;
            bool flat = b.size.y < 1f && area < 2500f;
            if (!flat && area > 30f) continue;
            if (area < 0.04f) continue;
            b.Expand(new Vector3(0.4f, 0f, 0.4f));
            list.Add(b);
        }
        return list;
    }

    // Terrain, or a large invisible collider used as ground or a level boundary slab (Level_04 GroundCollider,
    // the tutorial's Boundary_01). Thin invisible walls stay blockers because their footprint is small.
    static bool IsGround(Collider c)
    {
        if (c is TerrainCollider) return true;
        if (c.GetComponent<Renderer>() != null) return false;
        var b = c.bounds;
        return b.size.x * b.size.z > 400f;
    }

    // Why candidate spots were turned down, logged per scene so empty levels can be diagnosed.
    static readonly Dictionary<string, int> rejects = new();
    static void Reject(string why) => rejects[why] = rejects.TryGetValue(why, out int n) ? n + 1 : 1;
    static string RejectLog() => string.Join(", ", rejects.OrderByDescending(r => r.Value).Take(6).Select(r => r.Key + "=" + r.Value));

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new(p.x, p.z), A = new(a.x, a.z), B = new(b.x, b.z);
        var ab = B - A;
        float t = ab.sqrMagnitude < 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
        return Vector2.Distance(P, A + ab * t);
    }

    // Is there open, flat terrain here with nothing else on it, away from the player's working routes?
    static bool IsFree(Vector3 p, Vector3 half, float yaw, float routeClearance, Anchors a, List<Bounds> blockers, out Vector3 ground)
    {
        ground = p;
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) { Reject("noGround"); return false; }
        var hit = hits.OrderBy(h => h.distance).First();
        if (!IsGround(hit.collider)) { Reject("on:" + hit.collider.name); return false; }
        if (hit.normal.y < 0.97f) { Reject("slope"); return false; }
        ground = hit.point;

        // Keep the spawn, the stations and the walk between stations and house clear.
        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(a.spawn.x, a.spawn.z)) < routeClearance + 2f) { Reject("spawn"); return false; }
        if (a.hasUtility && DistToSegment(p, a.utility, a.house) < routeClearance) { Reject("route"); return false; }
        if (a.hasUtility && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(a.utility.x, a.utility.z)) < routeClearance + 3f) { Reject("stations"); return false; }

        var rot = Quaternion.Euler(0, yaw, 0);
        var center = ground + Vector3.up * (half.y + 0.15f);
        // Triggers are ignored: tutorial steps and level zones are big trigger volumes over open ground.
        if (Physics.OverlapBox(center, half + new Vector3(0.6f, 0f, 0.6f), rot, ~0, QueryTriggerInteraction.Ignore).FirstOrDefault(c => !IsGround(c)) is Collider blocker)
        {
            Reject("hits:" + blocker.name);
            return false;
        }

        float reach = Mathf.Max(half.x, half.z) + 0.3f;
        foreach (var b in blockers)
        {
            if (b.max.y < ground.y - 0.3f || b.min.y > ground.y + 3f) continue;
            if (p.x > b.min.x - reach && p.x < b.max.x + reach && p.z > b.min.z - reach && p.z < b.max.z + reach) { Reject("visual"); return false; }
        }
        return true;
    }

    // Free spots on rings around a centre, nearest first.
    static List<(Vector3 pos, float yaw)> FreeSpots(Anchors a, Vector3 around, float rMin, float rMax, Vector3 half, float routeClearance, List<Bounds> blockers)
    {
        var found = new List<(Vector3, float)>();
        for (float r = rMin; r <= rMax; r += 1.5f)
        {
            int steps = Mathf.Max(8, Mathf.RoundToInt(r * 2.5f));
            for (int k = 0; k < steps; k++)
            {
                float ang = (k + (r % 3f) * 0.3f) * Mathf.PI * 2f / steps;
                var p = around + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * r;
                // Long side faces the house.
                float yaw = Mathf.Atan2(around.x - p.x, around.z - p.z) * Mathf.Rad2Deg;
                if (IsFree(p, half, yaw, routeClearance, a, blockers, out var ground)) found.Add((ground, yaw));
            }
        }
        return found;
    }

    static int AddLevelDressing(Material foliage, Anchors a, List<Bounds> blockers)
    {
        var holder = AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(MeshHolderPath);
        var planterSize = new Vector3(1.5f, 0.42f, 0.55f);
        var crateSize = new Vector3(0.8f, 0.8f, 0.8f);
        var planterMesh = MakeBoxy("Planter", planterSize, true, 901);
        var crateMesh = MakeBoxy("Crate", crateSize, false, 0);
        AssetDatabase.AddObjectToAsset(planterMesh, holder);
        AssetDatabase.AddObjectToAsset(crateMesh, holder);

        var root = new GameObject("StyleTest_Dressing");
        int count = 0;
        void Place(Mesh m, Vector3 size, string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = foliage;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, size.y * 0.5f, 0);
            box.size = size;
            var carve = go.AddComponent<NavMeshObstacle>();
            carve.shape = NavMeshObstacleShape.Box;
            carve.center = box.center;
            carve.size = size;
            carve.carving = true;
            Physics.SyncTransforms();
            count++;
        }

        // Three pairs of planters, side by side, spread around the house.
        var pairHalf = new Vector3(planterSize.x + 0.1f, planterSize.y * 0.5f, planterSize.z * 0.5f);
        var placed = new List<Vector3>();
        foreach (var (pos, yaw) in FreeSpots(a, a.house, 7f, 28f, pairHalf, 3.5f, blockers))
        {
            if (placed.Count >= 3) break;
            if (placed.Any(p => Vector3.Distance(p, pos) < 7f)) continue;
            if (!IsFree(pos, pairHalf, yaw, 3.5f, a, blockers, out _)) continue; // earlier placements may now cover it
            var side = Quaternion.Euler(0, yaw, 0) * Vector3.right * (planterSize.x * 0.5f + 0.1f);
            Place(planterMesh, planterSize, "Planter", pos - side, yaw);
            Place(planterMesh, planterSize, "Planter", pos + side, yaw);
            placed.Add(pos);
        }

        // One crate stack by the stations (or the house when a scene has none).
        var stack = new[] { (new Vector3(0, 0, 0), 12f), (new Vector3(0.9f, 0, 0.3f), -8f), (new Vector3(0.4f, 0, -0.75f), 30f) };
        var stackHalf = new Vector3(1.3f, 0.45f, 1.3f);
        var crateSpots = FreeSpots(a, a.utility, a.hasUtility ? 5f : 6f, 14f, stackHalf, 2.5f, blockers);
        if (crateSpots.Count > 0)
        {
            var (c, yaw) = crateSpots[0];
            var rot = Quaternion.Euler(0, yaw, 0);
            var terrain = Object.FindFirstObjectByType<Terrain>();
            foreach (var (offset, turn) in stack)
            {
                var p = c + rot * offset;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                Place(crateMesh, crateSize, "Crate", p, yaw + turn);
            }
        }
        return count;
    }
}
