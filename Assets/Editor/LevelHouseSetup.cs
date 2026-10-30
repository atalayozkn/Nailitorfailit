using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Side = RoomLayout.Side;
using Pattern = RoomMaterialRecipe.Pattern;

// Places the house (room prefabs) on HouseCenter and the station row on UtilityCenter for the 5 levels.
// Re-runnable: it rebuilds the "House" and "Stations" roots it owns and removes the old loose stations.
// Tools > NailIt > Setup Level Houses (all 5) / Setup House In Active Level
public static class LevelHouseSetup
{
    private const string RoomsDir = "Assets/Prefabs/Rooms/";
    private const string HouseRootName = "House";
    private const string StationsRootName = "Stations";

    #region Data

    private class RoomDef
    {
        public string prefab;
        public string label;
        public Vector2 center;            // house-local, front door side is -Z
        public List<Side> openSides = new List<Side>();
        public List<RoomLayout.DoorPlacement> doors = new List<RoomLayout.DoorPlacement>();
        public Pattern pattern;
        public CarriableType primary;
        public CarriableType secondary;
        public float ratio = 0.3f;
    }

    private class LevelDef
    {
        public string scene;
        public bool houseOpenPositiveZ;   // which side of the house zone faces the road
        public bool stationsOpenPositiveZ;
        public bool mirror;               // flip the arrangement on X for variety
        public bool hasGlass;
        public int seed;
        public List<RoomDef> rooms;
    }

    private struct StationDef
    {
        public string prefab;
        public float x;
        public float y;
        public float rotY;
        public StationDef(string prefab, float x, float y, float rotY) { this.prefab = prefab; this.x = x; this.y = y; this.rotY = rotY; }
    }

    // Demo row, x relative to UtilityCenter.
    private static readonly StationDef[] BaseStations =
    {
        new StationDef("Assets/Prefabs/WorkStation/Station_WoodCutter.prefab", -3.35f, 0.61f, 0f),
        new StationDef("Assets/Prefabs/WorkStation/Mixer.prefab", -1.75f, 0.08f, 0f),
        new StationDef("Assets/Prefabs/Spawners/Spawner_Wood.prefab", 0.45f, 0.60f, 0f),
        new StationDef("Assets/Prefabs/Spawners/Spawner_Cement.prefab", 1.95f, 0.70f, 0f),
        new StationDef("Assets/Prefabs/Spawners/Spawner_Brick.prefab", 3.35f, 1.37f, 180f),
    };
    private static readonly StationDef GlassStation = new StationDef("Assets/Prefabs/Spawners/GlassSpawner.prefab", 4.75f, 0.60f, 0f);

    private static RoomLayout.DoorPlacement Door(Side side, int slot, bool front = false)
    {
        return new RoomLayout.DoorPlacement { side = side, slotIndex = slot, isFrontDoor = front };
    }

    // L shape: living on the right, bedroom + bathroom stacked on the left.
    private static List<RoomDef> LShape(RoomDef living, RoomDef bed, RoomDef bath)
    {
        living.prefab = "Room_3x6"; living.center = new Vector2(1.5f, 0f);
        living.doors = new List<RoomLayout.DoorPlacement> { Door(Side.South, 1, true), Door(Side.West, 1), Door(Side.West, 4) };
        bed.prefab = "Room_3x3"; bed.center = new Vector2(-1.5f, 1.5f); bed.openSides = new List<Side> { Side.East };
        bath.prefab = "Room_3x3_B"; bath.center = new Vector2(-1.5f, -1.5f); bath.openSides = new List<Side> { Side.East, Side.North };
        return new List<RoomDef> { living, bed, bath };
    }

    // T shape: living in the middle, bedroom and bathroom on the back corners.
    private static List<RoomDef> TShape(RoomDef living, RoomDef bed, RoomDef bath)
    {
        living.prefab = "Room_3x6"; living.center = new Vector2(0f, 0f);
        living.doors = new List<RoomLayout.DoorPlacement> { Door(Side.South, 1, true), Door(Side.West, 4), Door(Side.East, 4) };
        bed.prefab = "Room_3x3"; bed.center = new Vector2(-3f, 1.5f); bed.openSides = new List<Side> { Side.East };
        bath.prefab = "Room_3x3_B"; bath.center = new Vector2(3f, 1.5f); bath.openSides = new List<Side> { Side.West };
        return new List<RoomDef> { living, bed, bath };
    }

    // 4 rooms: L shape plus a second bedroom on the living room's other side (back or front).
    private static List<RoomDef> FourRooms(RoomDef living, RoomDef bed, RoomDef bath, RoomDef bed2, bool bed2AtFront)
    {
        var rooms = LShape(living, bed, bath);
        living.center = new Vector2(0f, 0f);
        bed.center = new Vector2(-3f, 1.5f);
        bath.center = new Vector2(-3f, -1.5f);
        bed2.prefab = "Room_3x3"; bed2.center = new Vector2(3f, bed2AtFront ? -1.5f : 1.5f); bed2.openSides = new List<Side> { Side.West };
        living.doors.Add(Door(Side.East, bed2AtFront ? 1 : 4));
        rooms.Add(bed2);
        return rooms;
    }

    private static RoomDef R(string label, Pattern pattern, CarriableType primary, CarriableType secondary = CarriableType.Brick, float ratio = 0.3f)
    {
        return new RoomDef { label = label, pattern = pattern, primary = primary, secondary = secondary, ratio = ratio };
    }

    private static List<LevelDef> Levels()
    {
        const CarriableType W = CarriableType.Wood, B = CarriableType.Brick, G = CarriableType.Glass;
        return new List<LevelDef>
        {
            // L1: Demo-like intro, wood + brick only.
            new LevelDef { scene = "Assets/Scenes/Level_01_TARIK.unity", houseOpenPositiveZ = false, seed = 1,
                rooms = LShape(R("LivingRoom", Pattern.AllPrimary, W), R("Bedroom", Pattern.AllPrimary, B), R("Bathroom", Pattern.Checker, W, B)) },
            // L2: glass is introduced in the bathroom.
            new LevelDef { scene = "Assets/Scenes/Level_02_TARIK.unity", houseOpenPositiveZ = true, hasGlass = true, seed = 2,
                rooms = TShape(R("LivingRoom", Pattern.Checker, W, B), R("Bedroom", Pattern.AllPrimary, B), R("Bathroom", Pattern.Random, B, G, 0.3f)) },
            // L3: mirrored L, more glass.
            new LevelDef { scene = "Assets/Scenes/Level_03_TARIK.unity", houseOpenPositiveZ = true, mirror = true, hasGlass = true, seed = 3,
                rooms = LShape(R("LivingRoom", Pattern.HalfSplit, W, B), R("Bedroom", Pattern.Checker, B, G), R("Bathroom", Pattern.Random, B, G, 0.5f)) },
            // L4: four rooms.
            new LevelDef { scene = "Assets/Scenes/Level_04_TARIK.unity", houseOpenPositiveZ = false, hasGlass = true, seed = 4,
                rooms = FourRooms(R("LivingRoom", Pattern.Random, W, G, 0.3f), R("Bedroom", Pattern.Checker, W, B), R("Bathroom", Pattern.Border, B, G), R("Bedroom2", Pattern.AllPrimary, B), false) },
            // L5: four rooms, mirrored, glass heavy.
            new LevelDef { scene = "Assets/Scenes/Level_05_TARIK.unity", houseOpenPositiveZ = true, mirror = true, hasGlass = true, seed = 5,
                rooms = FourRooms(R("LivingRoom", Pattern.Checker, B, G), R("Bedroom", Pattern.HalfSplit, W, G), R("Bathroom", Pattern.Border, B, G), R("Bedroom2", Pattern.Random, B, G, 0.4f), true) },
        };
    }

    #endregion

    #region Menu

    [MenuItem("Tools/NailIt/Setup Level Houses (all 5)")]
    public static void SetupAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var level in Levels())
        {
            var scene = EditorSceneManager.OpenScene(level.scene, OpenSceneMode.Single);
            Setup(level);
            EditorSceneManager.SaveScene(scene);
        }
    }

    [MenuItem("Tools/NailIt/Setup House In Active Level")]
    public static void SetupActive()
    {
        string path = EditorSceneManager.GetActiveScene().path;
        var level = Levels().FirstOrDefault(l => l.scene == path);
        if (level == null)
        {
            Debug.LogWarning("LevelHouseSetup: active scene is not one of the 5 level scenes.");
            return;
        }
        Setup(level);
    }

    // Sets up one level in its (already open) scene. Returns false when markers are missing.
    public static bool SetupScene(string scenePath)
    {
        var level = Levels().FirstOrDefault(l => l.scene == scenePath);
        return level != null && Setup(level);
    }

    #endregion

    #region Setup

    private static bool Setup(LevelDef level)
    {
        var scene = EditorSceneManager.GetSceneByPath(level.scene);
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var houseMarker = all.FirstOrDefault(t => t.name == "HouseCenter");
        var utilityMarker = all.FirstOrDefault(t => t.name == "UtilityCenter");
        if (houseMarker == null || utilityMarker == null)
        {
            Debug.LogError($"LevelHouseSetup: {level.scene} needs HouseCenter and UtilityCenter markers.");
            return false;
        }

        BuildHouse(scene, level, houseMarker);
        BuildStations(scene, level, utilityMarker, all);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"LevelHouseSetup: {scene.name} ready ({level.rooms.Count} rooms).");
        return true;
    }

    private static void BuildHouse(UnityEngine.SceneManagement.Scene scene, LevelDef level, Transform marker)
    {
        DestroyRoot(scene, HouseRootName);
        var house = new GameObject(HouseRootName);
        EditorSceneManager.MoveGameObjectToScene(house, scene);

        // Small seeded jitter so the house never sits exactly in the same spot.
        var rng = new System.Random(level.seed);
        var jitter = new Vector3((float)(rng.NextDouble() - 0.5) * 1.0f, 0f, (float)(rng.NextDouble() - 0.5) * 1.0f);
        house.transform.position = marker.position + jitter;
        // Front door is on house-local -Z, turn it toward the road.
        house.transform.rotation = Quaternion.Euler(0f, level.houseOpenPositiveZ ? 180f : 0f, 0f);

        foreach (var def in level.rooms)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomsDir + def.prefab + ".prefab");
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, house.transform);
            room.name = def.label;

            Vector2 c = def.center;
            var openSides = def.openSides;
            var doors = def.doors;
            if (level.mirror)
            {
                c.x = -c.x;
                openSides = openSides.Select(MirrorSide).ToList();
                // North/South slots run along X, so their index flips too.
                int width = room.GetComponent<RoomLayout>().Width;
                doors = doors.Select(d => Door(MirrorSide(d.side), d.side == Side.North || d.side == Side.South ? width - 1 - d.slotIndex : d.slotIndex, d.isFrontDoor)).ToList();
            }
            room.transform.localPosition = new Vector3(c.x, 0f, c.y);
            room.transform.localRotation = Quaternion.identity;

            room.GetComponent<RoomLayout>().Configure(openSides, doors);
            var recipe = room.GetComponent<RoomMaterialRecipe>();
            recipe.Setup(def.pattern, def.primary, def.secondary, def.ratio, level.seed * 10 + level.rooms.IndexOf(def));
            // Script changes on prefab instances are only kept as overrides when recorded.
            PrefabUtility.RecordPrefabInstancePropertyModifications(recipe);
            PrefabUtility.RecordPrefabInstancePropertyModifications(room.GetComponent<RoomLayout>());
        }
    }

    private static void BuildStations(UnityEngine.SceneManagement.Scene scene, LevelDef level, Transform marker, List<Transform> all)
    {
        // Remove the loose stations placed before this tool existed.
        string[] loose = { "Station_WoodCutter", "Mixer", "Spawner_Wood", "Spawner_Cement", "Spawner_Brick", "GlassSpawner" };
        foreach (var t in all.Where(t => t != null && loose.Contains(t.name) && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && t.root.name != StationsRootName).ToList())
            Object.DestroyImmediate(t.gameObject);

        DestroyRoot(scene, StationsRootName);
        var root = new GameObject(StationsRootName);
        EditorSceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = marker.position;
        root.transform.rotation = Quaternion.Euler(0f, level.stationsOpenPositiveZ ? 180f : 0f, 0f);

        var stations = BaseStations.ToList();
        if (level.hasGlass) stations.Add(GlassStation);
        // Keep the row centered on the marker.
        float offset = -(stations.Min(s => s.x) + stations.Max(s => s.x)) * 0.5f;

        foreach (var s in stations)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefab);
            if (prefab == null) { Debug.LogWarning("LevelHouseSetup: missing " + s.prefab); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.transform.localPosition = new Vector3(s.x + offset, s.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, s.rotY, 0f);
        }
    }

    private static Side MirrorSide(Side side)
    {
        return side == Side.East ? Side.West : side == Side.West ? Side.East : side;
    }

    private static void DestroyRoot(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects().Where(g => g.name == name).ToList()) Object.DestroyImmediate(go);
    }

    #endregion
}
