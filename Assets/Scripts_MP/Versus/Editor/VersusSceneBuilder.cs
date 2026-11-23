using System.Collections.Generic;
using System.Linq;
using ItemScript;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Builds the 2-player versus test map from a copy of Level_05, so the art matches the levels.
// The map is mirrored on the X axis: player A works on the west half, player B on the east half,
// houses south of the road, depots north of it, one shop column on the centre line.
// Re-runnable: it rebuilds the scene and the versus prefabs from scratch every time.
// Tools > NailIt > Versus > Build Versus Test Scene
public static class VersusSceneBuilder
{
    private const string SourceScene = "Assets/Scenes/Level_05.unity";
    private const string ScenePath = "Assets/Scenes/VersusMultiplayer_Test.unity";
    private const string PrefabDir = "Assets/Prefabs/MP/Versus";
    private const string BatPath = PrefabDir + "/BaseballBat_MP.prefab";
    private const string BaitPath = PrefabDir + "/DogBait_MP.prefab";
    private const string TruckPath = PrefabDir + "/Car_Active_OilTruck_MP.prefab";
    private const string FencePrefab = "Assets/Prefabs/SceneSetup/Environment/Fence.prefab";

    // Layout (world space, mirrored on X).
    private const float HouseX = 13f, HouseZ = -10.5f;
    private const float StationsX = 10f, StationsZ = 7.5f;
    private const float FenceStep = 1.44f, PadBackZ = 13.4f;
    private const int BaitCost = 40, BatCost = 60;
    private const float MatchSeconds = 900f; // 15 minutes

    private static readonly Rect PlayRect = Rect.MinMaxRect(-18.5f, -17.5f, 18.5f, 22f);

    [MenuItem("Tools/NailIt/Versus/Build Versus Test Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(SourceScene, ScenePath))
        {
            Debug.LogError("VersusSceneBuilder: could not copy " + SourceScene);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject bat = BuildBat();
        GameObject bait = BuildBait();
        GameObject truck = BuildOilTruck();

        CleanScene(scene);
        MeasureGround();

        LevelHouseSetup.BuildVersusSide(scene, "_A", new Vector3(-HouseX, groundY, HouseZ), new Vector3(-StationsX, groundY, StationsZ), false,
            out Transform houseA, out Transform stationsA, out List<ElectricalHazard_Prototype> socketsA);
        LevelHouseSetup.BuildVersusSide(scene, "_B", new Vector3(HouseX, groundY, HouseZ), new Vector3(StationsX, groundY, StationsZ), true,
            out Transform houseB, out Transform stationsB, out List<ElectricalHazard_Prototype> socketsB);

        int piles = BuildPiles(stationsA) + BuildPiles(stationsB);

        Transform root = new GameObject("--------VERSUS-------").transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, scene);

        BuildFences(root);
        BuildShops(root, bat, bait);

        VersusMatch match = new GameObject("VersusMatch").AddComponent<VersusMatch>();
        match.transform.SetParent(root, false);
        SerializedObject so = new SerializedObject(match);
        ConfigureSide(so, 0, "Blue", new Color(0.25f, 0.55f, 1f), houseA, stationsA, socketsA, root, -1f);
        ConfigureSide(so, 1, "Orange", new Color(1f, 0.55f, 0.2f), houseB, stationsB, socketsB, root, 1f);
        InputActionReference use = FindUseAction() as InputActionReference;
        so.FindProperty("controls").objectReferenceValue = use != null ? use.asset : null;
        so.FindProperty("oilTruckPrefab").objectReferenceValue = truck;
        SetArray(so.FindProperty("networkPrefabs"), new Object[] { bat, bait, truck });
        SetArray(so.FindProperty("lanes"), ConfigureTraffic(scene));
        so.ApplyModifiedPropertiesWithoutUndo();

        ConfigureManagers(scene);
        int surfaces = BakeNavMesh(scene);
        AddToBuildSettings();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"VersusSceneBuilder: {ScenePath} built ({piles} depot piles, sockets {socketsA.Count}+{socketsB.Count}, {surfaces} navmesh surfaces).");
    }

    // The copied scene still points at Level_05's baked navmesh (old house, gas station). Bake this layout's own.
    private static int BakeNavMesh(Scene scene)
    {
        const string folder = "Assets/Scenes/VersusMultiplayer_Test";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scenes", "VersusMultiplayer_Test");

        int count = 0;
        foreach (Unity.AI.Navigation.NavMeshSurface surface in scene.GetRootGameObjects()
                     .SelectMany(r => r.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(false)))
        {
            surface.BuildNavMesh();
            if (surface.navMeshData == null) continue;

            string path = $"{folder}/NavMesh-{count}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
            EditorUtility.SetDirty(surface);
            count++;
        }

        return count;
    }

    // Level_05 kept its house and stations on bare terrain (y 0). Here both players build on the asphalt slabs,
    // which lie higher; everything placed by this tool is lifted to that height so nothing is buried in the road.
    private static float groundY;

    private static void MeasureGround()
    {
        Physics.SyncTransforms();
        Vector2[] points = { new Vector2(-HouseX, HouseZ), new Vector2(HouseX, HouseZ), new Vector2(-StationsX, StationsZ), new Vector2(StationsX, StationsZ) };
        float min = float.MaxValue, max = float.MinValue;

        foreach (Vector2 point in points)
        {
            if (!Physics.Raycast(new Vector3(point.x, 6f, point.y), Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
            min = Mathf.Min(min, hit.point.y);
            max = Mathf.Max(max, hit.point.y);
        }

        groundY = max > float.MinValue ? max : 0f;
        if (max - min > 0.02f) Debug.LogWarning($"VersusSceneBuilder: the ground under the two sides differs by {max - min:0.00} m.");
    }

    #region Scene cleanup

    private static void CleanScene(Scene scene)
    {
        string[] removeRoots = { "House", "House_Old", "Stations", "HouseCenter", "UtilityCenter", "InfoPopup" };
        string[] disableRoots = { "Hazard_Controller", "Aid_Controller", "Rain_Controller" };

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (removeRoots.Contains(go.name)) Object.DestroyImmediate(go);
            else if (disableRoots.Contains(go.name)) go.SetActive(false);
            else if (go.name.Contains("STUFF")) DestroyChildren(go.transform, _ => true);
            else if (go.name.Contains("STATIC ENVIRONMENT")) CleanEnvironment(go.transform);
        }
    }

    private static void CleanEnvironment(Transform environment)
    {
        foreach (Transform group in environment)
        {
            switch (group.name)
            {
                case "benzinlik":
                case "dekorasyon":
                    DestroyChildren(group, _ => true);
                    break;
                case "Fences":
                    DestroyChildren(group, b => b.center.z > 0f);
                    break;
                case "Trees":
                case "Grass":
                    DestroyChildren(group, b => b.center.z > 5f && Mathf.Abs(b.center.x) < 26f);
                    break;
                case "Arabalar":
                    DestroyChildren(group, b => Mathf.Abs(b.center.x) < 26f && Mathf.Abs(b.center.z) < 14f);
                    break;
                case "evler":
                    DestroyChildren(group, b => PlayRect.Overlaps(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z)));
                    break;
                case "sokak lamba":
                    Symmetrize(group, b => b.center.z > 0f, 18f);
                    Symmetrize(group, b => b.center.z < 0f, 8.5f);
                    break;
                case "asfalt":
                    MirrorDepotSlab(group);
                    break;
                default:
                    if (group.name.StartsWith("yang")) Symmetrize(group, b => b.center.z < 0f, 19f);
                    break;
            }
        }
    }

    private static bool TryBounds(Transform t, out Bounds bounds)
    {
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        bounds = renderers.Length > 0 ? renderers[0].bounds : default;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return renderers.Length > 0;
    }

    private static void DestroyChildren(Transform group, System.Func<Bounds, bool> match)
    {
        foreach (Transform child in group.Cast<Transform>().ToList())
        {
            if (!TryBounds(child, out Bounds bounds))
            {
                if (group.name.Contains("STUFF") || match(new Bounds(child.position, Vector3.zero))) Object.DestroyImmediate(child.gameObject);
                continue;
            }

            if (match(bounds)) Object.DestroyImmediate(child.gameObject);
        }
    }

    // Thin street furniture near the play area is asymmetric in Level_05: keep one mirrored pair, drop the rest.
    private static void Symmetrize(Transform group, System.Func<Bounds, bool> filter, float x)
    {
        List<Transform> near = new List<Transform>();
        foreach (Transform child in group)
        {
            if (TryBounds(child, out Bounds bounds) && Mathf.Abs(bounds.center.x) < 22f && filter(bounds)) near.Add(child);
        }

        for (int i = 0; i < near.Count; i++)
        {
            if (i >= 2)
            {
                Object.DestroyImmediate(near[i].gameObject);
                continue;
            }

            TryBounds(near[i], out Bounds bounds);
            float targetX = i == 0 ? -x : x;
            near[i].position += new Vector3(targetX - bounds.center.x, 0f, 0f);
            if (PrefabUtility.IsPartOfPrefabInstance(near[i])) PrefabUtility.RecordPrefabInstancePropertyModifications(near[i]);
        }
    }

    // Player A's depot stands on the old gas station slab (x -18..0, 0.2 m above the terrain). In Level_05 the same
    // area on the east is partly bare terrain and sidewalk, so player B gets a copy of that slab: same ground for both.
    private static void MirrorDepotSlab(Transform group)
    {
        Transform template = null;
        Bounds source = default;
        foreach (Transform child in group)
        {
            if (!TryBounds(child, out Bounds bounds)) continue;
            if (Mathf.Abs(bounds.min.x + 18f) > 1.5f || Mathf.Abs(bounds.max.x) > 1.5f || Mathf.Abs(bounds.min.z - 3f) > 1.5f) continue;

            template = child;
            source = bounds;
        }

        if (template == null)
        {
            Debug.LogWarning("VersusSceneBuilder: west depot slab not found, the east depot ground was not levelled.");
            return;
        }

        GameObject copy = Object.Instantiate(template.gameObject, group);
        copy.name = template.name + "_VersusEast";
        // Slightly lower so it never flickers against the asphalt it overlaps.
        copy.transform.position = template.position + new Vector3(-source.min.x - source.max.x, -0.004f, 0f);
    }

    #endregion

    #region Depots, fences, shops

    // Swaps every press-to-take spawner of a station row for a timed pile.
    private static int BuildPiles(Transform stations)
    {
        int count = 0;

        foreach (ObjectSpawner spawner in stations.GetComponentsInChildren<ObjectSpawner>(true))
        {
            GameObject go = spawner.gameObject;
            Object item = new SerializedObject(spawner).FindProperty("objectPrefab").objectReferenceValue;

            Transform[] slots = new Transform[3];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new GameObject("PileSlot_" + i).transform;
                slots[i].SetParent(go.transform, false);
                slots[i].position = new Vector3(go.transform.position.x + (i - 1) * 0.45f, 0.7f, stations.position.z - 1.3f);
                slots[i].rotation = Quaternion.identity;
            }

            foreach (Spawner_MP networkSpawner in go.GetComponents<Spawner_MP>()) Object.DestroyImmediate(networkSpawner);
            Object.DestroyImmediate(spawner);

            VersusPileSpawner pile = go.AddComponent<VersusPileSpawner>();
            SerializedObject so = new SerializedObject(pile);
            so.FindProperty("itemPrefab").objectReferenceValue = item;
            SetArray(so.FindProperty("slots"), slots);
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }

        return count;
    }

    private static void BuildFences(Transform root)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FencePrefab);
        if (prefab == null)
        {
            Debug.LogWarning("VersusSceneBuilder: missing " + FencePrefab);
            return;
        }

        Transform parent = new GameObject("Fences").transform;
        parent.SetParent(root, false);

        // One back fence behind both depots and the shop, one side fence on each outer edge. The front is open to the road.
        for (int i = -11; i <= 11; i++) PlaceFence(prefab, parent, new Vector3(i * FenceStep, groundY, PadBackZ), 0f);

        float sideX = 11.5f * FenceStep;
        for (int i = 0; i < 5; i++)
        {
            float z = PadBackZ - FenceStep * (i + 0.5f);
            PlaceFence(prefab, parent, new Vector3(-sideX, groundY, z), 90f);
            PlaceFence(prefab, parent, new Vector3(sideX, groundY, z), 90f);
        }
    }

    private static void PlaceFence(GameObject prefab, Transform parent, Vector3 position, float rotY)
    {
        GameObject fence = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        fence.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotY, 0f));
    }

    // One column of stalls on the centre line, so every stall is the same distance from both players.
    private static void BuildShops(Transform root, GameObject bat, GameObject bait)
    {
        Transform parent = new GameObject("Shops").transform;
        parent.SetParent(root, false);

        (string prefab, float y, string name, GameObject item, int cost, string label)[] stalls =
        {
            ("Assets/Prefabs/Shop/Shop_EnergyDrink.prefab", 1.2f, null, null, 0, null),
            ("Assets/Prefabs/Shop/Shop_Oil.prefab", -0.4f, null, null, 0, null),
            ("Assets/Prefabs/Shop/Shop_PetFood.prefab", -0.3f, null, null, 0, null),
            ("Assets/Prefabs/Shop/Shop_FireExtinguisher.prefab", -0.4f, null, null, 0, null),
            ("Assets/Prefabs/Shop/Shop_PetFood.prefab", -0.3f, "Shop_DogBait", bait, BaitCost, "Dog Bait"),
            ("Assets/Prefabs/Shop/Shop_EnergyDrink.prefab", 1.2f, "Shop_BaseballBat", bat, BatCost, "Baseball Bat"),
        };

        for (int i = 0; i < stalls.Length; i++)
        {
            var stall = stalls[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stall.prefab);
            if (prefab == null)
            {
                Debug.LogWarning("VersusSceneBuilder: missing " + stall.prefab);
                continue;
            }

            GameObject shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            shop.transform.SetPositionAndRotation(new Vector3(0f, stall.y + groundY, 6.4f + i * 0.95f), Quaternion.Euler(0f, i % 2 == 0 ? 90f : 270f, 0f));

            if (stall.item == null) continue;

            shop.name = stall.name;
            SerializedObject so = new SerializedObject(shop.GetComponent<Shop>());
            so.FindProperty("objectPrefab").objectReferenceValue = stall.item;
            so.FindProperty("cost").intValue = stall.cost;
            so.ApplyModifiedPropertiesWithoutUndo();

            TextMeshProUGUI label = shop.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                // The other stalls show only the name; these two also show the price, because the versus wallet is tight.
                label.text = stall.label.Replace(' ', '\n') + "\n" + stall.cost;
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }

            if (stall.item.GetComponent<VersusBat>() != null)
            {
                MeshFilter stand = shop.GetComponentInChildren<MeshFilter>(true);
                if (stand != null) SwapMesh(stand, "beyzbol sopas", true);
            }
        }
    }

    private static void ConfigureSide(SerializedObject match, int index, string label, Color color, Transform house, Transform stations,
        List<ElectricalHazard_Prototype> sockets, Transform root, float sign)
    {
        GameObject zone = new GameObject("DepotZone_" + label);
        zone.transform.SetParent(root, false);
        zone.transform.position = new Vector3(sign * StationsX, 1.5f + groundY, 8.7f);
        zone.layer = LayerMask.NameToLayer("Ignore Raycast");
        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(12.6f, 3f, 9.2f);

        Transform anchor = new GameObject("DogAnchor_" + label).transform;
        anchor.SetParent(root, false);
        anchor.SetPositionAndRotation(new Vector3(sign * 14.5f, 0.1f + groundY, 11.6f), Quaternion.Euler(0f, 180f, 0f));

        SerializedProperty side = match.FindProperty("sides").GetArrayElementAtIndex(index);
        side.FindPropertyRelative("label").stringValue = label;
        side.FindPropertyRelative("color").colorValue = color;
        side.FindPropertyRelative("buildingRoot").objectReferenceValue = house;
        side.FindPropertyRelative("stationsRoot").objectReferenceValue = stations;
        side.FindPropertyRelative("depotZone").objectReferenceValue = box;
        side.FindPropertyRelative("dogAnchor").objectReferenceValue = anchor;
        SetArray(side.FindPropertyRelative("sockets"), sockets.ToArray());
    }

    #endregion

    #region Managers

    private static Object[] ConfigureTraffic(Scene scene)
    {
        CarController_Prototype controller = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<CarController_Prototype>(true)).FirstOrDefault();
        if (controller == null) return new Object[0];

        // Level_05 ships with its traffic switched off; the versus map needs the cars and the oil truck.
        controller.enabled = true;
        EditorUtility.SetDirty(controller);

        SerializedObject so = new SerializedObject(controller);
        SerializedProperty truck = so.FindProperty("oilTruckPrefab");
        if (truck.objectReferenceValue == null)
        {
            truck.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Cars/Car_Active_OilTruck.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedProperty points = so.FindProperty("spawnPoints");

        // Level_05 ships with an empty lane list (its markers exist but are not assigned): wire them here.
        Transform markers = controller.transform.Find("SpawnTransforms");
        if (points.arraySize == 0 && markers != null)
        {
            SetArray(points, markers.Cast<Transform>().ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Object[] lanes = new Object[points.arraySize];
        for (int i = 0; i < lanes.Length; i++) lanes[i] = points.GetArrayElementAtIndex(i).objectReferenceValue;
        return lanes;
    }

    private static void ConfigureManagers(Scene scene)
    {
        LevelManager level = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelManager>(true)).FirstOrDefault();
        if (level != null)
        {
            // VersusMatch pays the room reward to the player who finished the room and decides the winner itself.
            SerializedObject so = new SerializedObject(level);
            so.FindProperty("roomCurrencyReward").intValue = 0;
            so.FindProperty("roomTimeReward").intValue = 0;
            so.FindProperty("onVictoryEvent.m_PersistentCalls.m_Calls").arraySize = 0;
            so.FindProperty("onDefeatEvent.m_PersistentCalls.m_Calls").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        GameTimeManager clock = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameTimeManager>(true)).FirstOrDefault();
        if (clock != null)
        {
            SerializedObject so = new SerializedObject(clock);
            so.FindProperty("maxRoundTime").floatValue = MatchSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    #endregion

    #region Prefabs

    private static GameObject Unpacked(string sourcePath, string name)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = name;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return instance;
    }

    private static GameObject Save(GameObject instance, string path)
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs/MP", "Versus");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    private static GameObject BuildBat()
    {
        GameObject bat = Unpacked("Assets/Prefabs/MP/Objects/EnergyDrink_MP.prefab", "BaseballBat_MP");
        Object.DestroyImmediate(bat.GetComponent<EnergyDrink_SP>());

        MeshFilter mesh = bat.GetComponentInChildren<MeshFilter>(true);
        SwapMesh(mesh, "beyzbol sopas", false);

        // The model comes in at the stand's display size; bring it down to a real bat (about 0.9 m).
        if (mesh != null && mesh.TryGetComponent(out Renderer batRenderer))
        {
            Vector3 size = batRenderer.bounds.size;
            float longest = Mathf.Max(size.x, size.y, size.z);
            if (longest > 0.01f) mesh.transform.localScale *= 0.9f / longest;
        }

        FitCollider(bat);

        CarriableObject_SP carriable = bat.GetComponent<CarriableObject_SP>();
        carriable.carriableType = CarriableType.Usable;

        Object useAction = FindUseAction();
        if (useAction == null) Debug.LogWarning("VersusSceneBuilder: Use input action not found, assign VersusBat.useAction by hand.");

        SerializedObject so = new SerializedObject(bat.AddComponent<VersusBat>());
        so.FindProperty("carriable").objectReferenceValue = carriable;
        so.FindProperty("useAction").objectReferenceValue = useAction;
        so.FindProperty("visual").objectReferenceValue = mesh != null ? mesh.transform : null;
        so.FindProperty("ignoredLayers").intValue = LayerMask.GetMask("Ground");
        so.ApplyModifiedPropertiesWithoutUndo();

        return Save(bat, BatPath);
    }

    private static Object FindUseAction()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player_Package_Default.prefab");
        PlayerUseHandler useHandler = player != null ? player.GetComponentInChildren<PlayerUseHandler>(true) : null;
        return useHandler != null ? new SerializedObject(useHandler).FindProperty("useAction").objectReferenceValue : null;
    }

    private static GameObject BuildBait()
    {
        GameObject bait = Unpacked("Assets/Prefabs/MP/Objects/PetFood_MP.prefab", "DogBait_MP");
        bait.GetComponent<CarriableObject_SP>().carriableType = CarriableType.Usable;
        bait.AddComponent<VersusBait>();
        return Save(bait, BaitPath);
    }

    // The singleplayer oil truck made network-ready the same way Tools > MP builds the networked car.
    private static GameObject BuildOilTruck()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Cars/Car_Active_OilTruck.prefab");
        GameObject truck = (GameObject)PrefabUtility.InstantiatePrefab(source);
        truck.name = "Car_Active_OilTruck_MP";

        SerializedObject car = new SerializedObject(truck.GetComponent<Car_Active>());
        int playerMask = car.FindProperty("whatIsPlayer").intValue;
        float lifeTime = car.FindProperty("maxLifeTime").floatValue;
        car.FindProperty("whatIsPlayer").intValue = 0;
        car.ApplyModifiedPropertiesWithoutUndo();

        if (!truck.TryGetComponent(out NetworkIdentity _)) truck.AddComponent<NetworkIdentity>();

        NetworkTransformReliable sync = truck.AddComponent<NetworkTransformReliable>();
        sync.syncDirection = SyncDirection.ServerToClient;
        sync.syncScale = false;
        sync.interpolatePosition = true;
        sync.interpolateRotation = true;

        SerializedObject hit = new SerializedObject(truck.AddComponent<MpCarHitSync_MP>());
        hit.FindProperty("whatIsPlayer").intValue = playerMask;
        hit.FindProperty("serverLifeTime").floatValue = lifeTime > 0f ? lifeTime : 10f;
        hit.ApplyModifiedPropertiesWithoutUndo();

        return Save(truck, TruckPath);
    }

    // Replaces a mesh with a model from the artist's stand pack (same pack and scale as the shop items).
    private static void SwapMesh(MeshFilter target, string modelName, bool bucket)
    {
        if (target == null) return;

        foreach (string guid in AssetDatabase.FindAssets("beyzbol t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith(modelName) || file.Contains("kova") != bucket) continue;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MeshFilter source = model.GetComponentInChildren<MeshFilter>(true);
            if (source == null) continue;

            target.sharedMesh = source.sharedMesh;
            if (target.TryGetComponent(out MeshRenderer renderer) && source.TryGetComponent(out MeshRenderer sourceRenderer))
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target.GetComponent<MeshRenderer>());
            }
            return;
        }

        Debug.LogWarning("VersusSceneBuilder: model '" + modelName + "' not found, the old mesh was kept.");
    }

    private static void FitCollider(GameObject root)
    {
        if (!root.TryGetComponent(out BoxCollider box)) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        box.center = bounds.center;
        box.size = Vector3.Max(bounds.size, Vector3.one * 0.15f);
    }

    #endregion

    private static void SetArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
