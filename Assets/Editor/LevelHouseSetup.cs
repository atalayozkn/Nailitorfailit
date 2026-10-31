using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Side = RoomLayout.Side;
using Pattern = RoomMaterialRecipe.Pattern;

// Places the house (room prefabs) on HouseCenter and the station row + shops on UtilityCenter for the 5 levels,
// and sets up each level's hazards (sockets / pipes on Hazard_Controller, no rain) and MailMan aid.
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

    // A socket / pipe spot: socket sits on the inside of a wall slot, pipe under the room's floor.
    private class HazardSpot
    {
        public string room;
        public Side side;
        public int slot;
        public HazardSpot(string room, Side side = Side.North, int slot = 1) { this.room = room; this.side = side; this.slot = slot; }
    }

    private class LevelDef
    {
        public string scene;
        public bool houseOpenPositiveZ;   // which side of the house zone faces the road
        public bool stationsOpenPositiveZ;
        public bool mirror;               // flip the arrangement on X for variety
        public bool hasMixer;             // mixer + cement + brick piles
        public bool hasGlass;             // glass pile
        public bool hasFire;              // fire extinguisher sold in the shop
        public int seed;
        public List<RoomDef> rooms;
        public List<HazardSpot> sockets = new List<HazardSpot>();
        public List<HazardSpot> pipes = new List<HazardSpot>();
    }

    private struct StationDef
    {
        public string prefab;
        public float width;               // space the station takes in the row
        public float y;
        public float rotY;
        public StationDef(string prefab, float width, float y, float rotY) { this.prefab = prefab; this.width = width; this.y = y; this.rotY = rotY; }
    }

    // Demo station sizes / heights; the row is laid out left to right with only the stations a level needs.
    private static readonly StationDef WoodCutter = new StationDef("Assets/Prefabs/WorkStation/Station_WoodCutter.prefab", 1.6f, 0.61f, 0f);
    private static readonly StationDef MixerStation = new StationDef("Assets/Prefabs/WorkStation/Mixer.prefab", 2.2f, 0.08f, 0f);
    private static readonly StationDef WoodPile = new StationDef("Assets/Prefabs/Spawners/Spawner_Wood.prefab", 1.5f, 0.60f, 0f);
    private static readonly StationDef CementPile = new StationDef("Assets/Prefabs/Spawners/Spawner_Cement.prefab", 1.4f, 0.70f, 0f);
    private static readonly StationDef BrickPile = new StationDef("Assets/Prefabs/Spawners/Spawner_Brick.prefab", 1.4f, 1.37f, 180f);
    private static readonly StationDef GlassPile = new StationDef("Assets/Prefabs/Spawners/GlassSpawner.prefab", 1.4f, 0.60f, 0f);

    // Shop stands go in a small cluster in front of the row (Demo heights).
    private static readonly StationDef ShopEnergy = new StationDef("Assets/Prefabs/Shop/Shop_EnergyDrink.prefab", 0.9f, 1.1f, 180f);
    private static readonly StationDef ShopOil = new StationDef("Assets/Prefabs/Shop/Shop_Oil.prefab", 0.9f, -0.6f, 180f);
    private static readonly StationDef ShopPetFood = new StationDef("Assets/Prefabs/Shop/Shop_PetFood.prefab", 0.9f, -0.4f, 180f);
    private static readonly StationDef ShopExtinguisher = new StationDef("Assets/Prefabs/Shop/Shop_FireExtinguisher.prefab", 0.9f, -0.6f, 180f);
    private const float ShopRowZ = -1.7f;

    private const string SocketPrefab = "Assets/Prefabs/Props/Traps/Hazards/Electric_Socket.prefab";
    private const string PipePrefab = "Assets/Prefabs/Props/Traps/Hazards/WaterPipe.prefab";

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
            // L1: all-wood house. New: cars and shops.
            new LevelDef { scene = "Assets/Scenes/Level_01.unity", houseOpenPositiveZ = false, seed = 1,
                rooms = LShape(R("LivingRoom", Pattern.AllPrimary, W), R("Bedroom", Pattern.AllPrimary, W), R("Bathroom", Pattern.AllPrimary, W)) },
            // L2: wooden house again. New: electrical fire (sockets) + fire extinguisher shop.
            new LevelDef { scene = "Assets/Scenes/Level_02.unity", houseOpenPositiveZ = true, hasFire = true, seed = 2,
                rooms = TShape(R("LivingRoom", Pattern.AllPrimary, W), R("Bedroom", Pattern.AllPrimary, W), R("Bathroom", Pattern.AllPrimary, W)),
                sockets = { new HazardSpot("LivingRoom", Side.East, 1), new HazardSpot("Bedroom", Side.North, 1) } },
            // L3: wood + brick. New: mixer, cement and brick piles.
            new LevelDef { scene = "Assets/Scenes/Level_03.unity", houseOpenPositiveZ = true, mirror = true, hasMixer = true, hasFire = true, seed = 3,
                rooms = LShape(R("LivingRoom", Pattern.HalfSplit, W, B), R("Bedroom", Pattern.AllPrimary, B), R("Bathroom", Pattern.Checker, W, B)),
                sockets = { new HazardSpot("LivingRoom", Side.East, 3), new HazardSpot("Bedroom", Side.North, 1) } },
            // L4: four rooms. New: glass.
            new LevelDef { scene = "Assets/Scenes/Level_04.unity", houseOpenPositiveZ = false, hasMixer = true, hasGlass = true, hasFire = true, seed = 4,
                rooms = FourRooms(R("LivingRoom", Pattern.Random, W, G, 0.3f), R("Bedroom", Pattern.Checker, W, B), R("Bathroom", Pattern.Border, B, G), R("Bedroom2", Pattern.AllPrimary, B), false),
                sockets = { new HazardSpot("LivingRoom", Side.North, 1), new HazardSpot("Bathroom", Side.South, 1) } },
            // L5: four rooms, everything. New: leaking water pipes (no rain).
            new LevelDef { scene = "Assets/Scenes/Level_05.unity", houseOpenPositiveZ = true, mirror = true, hasMixer = true, hasGlass = true, hasFire = true, seed = 5,
                rooms = FourRooms(R("LivingRoom", Pattern.Checker, B, G), R("Bedroom", Pattern.HalfSplit, W, G), R("Bathroom", Pattern.Border, B, G), R("Bedroom2", Pattern.Random, W, B, 0.4f), true),
                sockets = { new HazardSpot("LivingRoom", Side.North, 1), new HazardSpot("Bedroom", Side.North, 1) },
                pipes = { new HazardSpot("Bathroom"), new HazardSpot("Bedroom2") } },
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

        var house = BuildHouse(scene, level, houseMarker);
        BuildStations(scene, level, utilityMarker, all);
        var hazards = BuildHazards(level, house);
        WireControllers(scene, hazards);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"LevelHouseSetup: {scene.name} ready ({level.rooms.Count} rooms).");
        return true;
    }

    private static Transform BuildHouse(UnityEngine.SceneManagement.Scene scene, LevelDef level, Transform marker)
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
        return house.transform;
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

        // Only the stations this level needs, laid out left to right and centered on the marker.
        var stations = new List<StationDef> { WoodCutter };
        if (level.hasMixer) stations.Add(MixerStation);
        stations.Add(WoodPile);
        if (level.hasMixer) { stations.Add(CementPile); stations.Add(BrickPile); }
        if (level.hasGlass) stations.Add(GlassPile);
        PlaceRow(root.transform, stations, 0f, 0f);

        // Shops: small cluster in front of the row, on the right so the spawn in front stays free.
        var shops = new List<StationDef> { ShopEnergy, ShopOil, ShopPetFood };
        if (level.hasFire) shops.Add(ShopExtinguisher);
        float shopsWidth = shops.Sum(s => s.width);
        // Pads are ~10m wide (fence at +-5) and the player spawns at x 0: keep the cluster inside x 1..5.
        PlaceRow(root.transform, shops, Mathf.Min(3f, 4.8f - shopsWidth * 0.5f), ShopRowZ);
    }

    // Places a row of prefabs along local X, centered on centerX.
    private static void PlaceRow(Transform parent, List<StationDef> row, float centerX, float z)
    {
        float x = centerX - row.Sum(s => s.width) * 0.5f;
        foreach (var s in row)
        {
            x += s.width * 0.5f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefab);
            if (prefab == null) Debug.LogWarning("LevelHouseSetup: missing " + s.prefab);
            else
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.localPosition = new Vector3(x, s.y, z);
                go.transform.localRotation = Quaternion.Euler(0f, s.rotY, 0f);
            }
            x += s.width * 0.5f;
        }
    }

    #endregion

    #region Hazards

    private struct HazardRefs
    {
        public List<ElectricalHazard_Prototype> sockets;
        public List<WaterHazard_Prototype> pipes;
    }

    // Sockets are mounted on the inside of a wall slot facing the room, pipes lie under the room floor.
    // They live under House/Hazards (not inside the room Structure, which is hidden until the room is done).
    private static HazardRefs BuildHazards(LevelDef level, Transform house)
    {
        var refs = new HazardRefs { sockets = new List<ElectricalHazard_Prototype>(), pipes = new List<WaterHazard_Prototype>() };
        if (level.sockets.Count == 0 && level.pipes.Count == 0) return refs;

        var parent = new GameObject("Hazards").transform;
        parent.SetParent(house, false);

        foreach (var spot in level.sockets)
        {
            var room = house.Find(spot.room);
            if (room == null) { Debug.LogWarning("LevelHouseSetup: no room " + spot.room); continue; }
            var side = level.mirror ? MirrorSide(spot.side) : spot.side;
            int width = room.GetComponent<RoomLayout>().Width;
            int slotIndex = level.mirror && (spot.side == Side.North || spot.side == Side.South) ? width - 1 - spot.slot : spot.slot;
            var slot = room.Find("Structure/Slots/" + side + "_" + slotIndex);
            if (slot == null) { Debug.LogWarning("LevelHouseSetup: no slot " + side + "_" + slotIndex + " in " + spot.room); continue; }

            Vector3 inward = room.TransformDirection(-SideNormal(side));
            var socket = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SocketPrefab), parent);
            socket.name = "Socket_" + spot.room;
            socket.transform.position = slot.position + inward * 0.12f + Vector3.up * 0.3f;
            socket.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
            refs.sockets.Add(socket.GetComponent<ElectricalHazard_Prototype>());
        }

        foreach (var spot in level.pipes)
        {
            var room = house.Find(spot.room);
            if (room == null) { Debug.LogWarning("LevelHouseSetup: no room " + spot.room); continue; }
            var pipe = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PipePrefab), parent);
            pipe.name = "Pipe_" + spot.room;
            pipe.transform.position = room.position + Vector3.up * 0.05f;
            pipe.transform.rotation = room.rotation;
            // The pipe mesh is modelled 3.4m away from its pivot; pull it under the trigger point.
            var mesh = pipe.transform.Find("Mesh");
            if (mesh != null)
            {
                var r = mesh.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x.bounds.size.sqrMagnitude > 0);
                if (r != null)
                {
                    Vector3 delta = pipe.transform.position + Vector3.down * 0.12f - r.bounds.center;
                    mesh.position += delta;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
                }
            }
            refs.pipes.Add(pipe.GetComponent<WaterHazard_Prototype>());
        }
        return refs;
    }

    private static Vector3 SideNormal(Side side)
    {
        switch (side)
        {
            case Side.North: return Vector3.forward;
            case Side.South: return Vector3.back;
            case Side.East: return Vector3.right;
            default: return Vector3.left;
        }
    }

    // Hazard_Controller fires the level's sockets/pipes (no rain), Aid_Controller sends the MailMan.
    private static void WireControllers(UnityEngine.SceneManagement.Scene scene, HazardRefs hazards)
    {
        var controllers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HazardController_Prototype>(true)).ToList();

        var hazardController = controllers.FirstOrDefault(c => c.name == "Hazard_Controller");
        if (hazardController != null)
        {
            var calls = new List<Object>();
            calls.AddRange(hazards.sockets);
            calls.AddRange(hazards.pipes);
            SetEvents(hazardController, calls.Select(t => (t, "OnTrigger", false)).ToList());
        }

        var aidController = controllers.FirstOrDefault(c => c.name == "Aid_Controller");
        var mailMan = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MailManController>(true)).FirstOrDefault();
        if (aidController != null && mailMan != null)
            SetEvents(aidController, new List<(Object, string, bool)> { (mailMan.gameObject, "SetActive", true) });
    }

    // Rewrites HazardController_Prototype.availableHazards: one UnityEvent per target.
    private static void SetEvents(HazardController_Prototype controller, List<(Object target, string method, bool boolArg)> calls)
    {
        var so = new SerializedObject(controller);
        var events = so.FindProperty("availableHazards");
        events.arraySize = calls.Count;
        for (int i = 0; i < calls.Count; i++)
        {
            var list = events.GetArrayElementAtIndex(i).FindPropertyRelative("m_PersistentCalls.m_Calls");
            list.arraySize = 1;
            var call = list.GetArrayElementAtIndex(0);
            var (target, method, boolArg) = calls[i];
            bool isBool = target is GameObject;
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").intValue = isBool ? 6 : 1; // 6 = bool argument, 1 = no argument
            call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue = boolArg;
            call.FindPropertyRelative("m_CallState").intValue = 2;          // RuntimeOnly
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region Utility

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
