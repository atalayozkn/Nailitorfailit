using System.IO;
using System.Linq;
using ItemScript;
using TMPro;
using UnityEditor;
using UnityEngine;

// Generates self-contained room prefabs (floor constructors + wall slots + roof) from the modular
// building parts. Re-run after changing a part: Tools > NailIt > Build Room Prefabs.
public static class RoomPrefabBuilder
{
    private const string PartsDir = "Assets/Prefabs/SceneSetup/Modular_Set_Building/";
    private const string OutputDir = "Assets/Prefabs/Rooms/";
    private const string FloorConstructPath = "Assets/Prefabs/Constructors/FloorConstruct.prefab";
    private const string RoomCanvasSourcePath = "Assets/Prefabs/SceneSetup/Empty_Room_Layout.prefab";
    private const string RoofModelPath = "Assets/Art/PANDANIN/ev parçaları/ev çatısı süslenmiş.fbx";

    // Wall panel offsets from the floor edge (inner panel stands on the tile edge, outer panel outside it).
    private const float InnerOffset = -0.035f;
    private const float OuterOffset = 0.05f;
    private const float RoofOverhang = 0.3f;

    [MenuItem("Tools/NailIt/Build Room Prefabs")]
    public static void BuildAll()
    {
        if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);

        Build("Room_3x3", 3, 3, "Plane.085");
        Build("Room_3x3_B", 3, 3, "Plane.001");
        Build("Room_3x6", 3, 6, "Plane.003");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Rebuilt prefabs reset slot visibility on instances, restore it from each RoomLayout.
        foreach (var layout in Object.FindObjectsByType<RoomLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None)) layout.Apply();
    }

    private static void Build(string prefabName, int width, int depth, string roofNode)
    {
        var root = new GameObject(prefabName);
        try
        {
            var roomController = root.AddComponent<RoomController>();
            var layout = root.AddComponent<RoomLayout>();
            layout.SetSize(width, depth);
            var recipe = root.AddComponent<RoomMaterialRecipe>();
            recipe.SetSize(width, depth);

            GameObject[] tiles = BuildTiles(root.transform, roomController, width, depth);

            var structure = new GameObject("Structure").transform;
            structure.SetParent(root.transform, false);
            float wallTop = BuildSlots(structure, width, depth);
            BuildRoof(structure, prefabName, width, depth, roofNode, wallTop);

            TextMeshProUGUI roomText = BuildCanvas(root.transform);

            var so = new SerializedObject(roomController);
            so.FindProperty("roomText").objectReferenceValue = roomText;
            so.FindProperty("wallStructure").objectReferenceValue = structure.gameObject;
            var tileProp = so.FindProperty("availableTiles");
            tileProp.arraySize = tiles.Length;
            for (int i = 0; i < tiles.Length; i++) tileProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            layout.Apply();

            PrefabUtility.SaveAsPrefabAsset(root, OutputDir + prefabName + ".prefab");
            Debug.Log($"RoomPrefabBuilder: built {prefabName} ({tiles.Length} tiles, wall top {wallTop:F2}m)");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    #region Tiles

    private static GameObject[] BuildTiles(Transform root, RoomController roomController, int width, int depth)
    {
        var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorConstructPath);
        var parent = new GameObject("Tiles").transform;
        parent.SetParent(root, false);

        var tiles = new GameObject[width * depth];
        for (int row = 0; row < depth; row++)
        {
            for (int col = 0; col < width; col++)
            {
                var center = new Vector3(col - (width - 1) * 0.5f, 0f, row - (depth - 1) * 0.5f);
                var tile = Place(floorPrefab, parent, center, 0f, "GhostMesh");
                tile.name = $"FloorConstruct_{col}_{row}";

                var so = new SerializedObject(tile.GetComponent<Constructor>());
                so.FindProperty("roomController").objectReferenceValue = roomController;
                so.ApplyModifiedPropertiesWithoutUndo();

                tiles[row * width + col] = tile;
            }
        }
        return tiles;
    }

    #endregion

    #region Walls

    private static float BuildSlots(Transform structure, int width, int depth)
    {
        var wallInner = Load("Wall_Inner");
        var wallOuter = Load("Wall_Outher");
        var doorInner = Load("Door_Inner");
        var doorOuter = Load("Door_Outher");

        var slots = new GameObject("Slots").transform;
        slots.SetParent(structure, false);

        float wallTop = 0f;
        foreach (RoomLayout.Side side in System.Enum.GetValues(typeof(RoomLayout.Side)))
        {
            int count = side == RoomLayout.Side.North || side == RoomLayout.Side.South ? width : depth;
            for (int i = 0; i < count; i++)
            {
                GetSlotFrame(side, i, width, depth, out Vector3 edgeCenter, out Vector3 outward, out float rotY);

                var slot = new GameObject(side + "_" + i).transform;
                slot.SetParent(slots, false);
                slot.localPosition = edgeCenter;

                var wall = new GameObject("Wall").transform;
                wall.SetParent(slot, false);
                var inner = Place(wallInner, wall, outward * InnerOffset, rotY, null, slot);
                var outer = Place(wallOuter, wall, outward * OuterOffset, rotY, null, slot);

                // Stretch the outer panels at the ends of north/south sides so the corners close.
                bool isEnd = i == 0 || i == count - 1;
                if (isEnd && (side == RoomLayout.Side.North || side == RoomLayout.Side.South))
                {
                    float dir = i == 0 ? -1f : 1f;
                    var s = outer.transform.localScale; s.x *= 1.1f; outer.transform.localScale = s;
                    outer.transform.localPosition += new Vector3(dir * 0.05f, 0f, 0f);
                }
                wallTop = Mathf.Max(wallTop, GetBounds(inner.transform, null).max.y, GetBounds(outer.transform, null).max.y);

                var di = Place(doorInner, slot, Vector3.zero, rotY, null, slot);
                di.name = "DoorInner";
                var dO = Place(doorOuter, slot, outward * 0.02f, rotY, null, slot);
                dO.name = "DoorOuter";
            }
        }
        return wallTop;
    }

    // Edge center (room local), outward normal and the rotation that makes a part's +Z face the room.
    private static void GetSlotFrame(RoomLayout.Side side, int i, int width, int depth, out Vector3 edgeCenter, out Vector3 outward, out float rotY)
    {
        float halfW = width * 0.5f, halfD = depth * 0.5f;
        float x = i - (width - 1) * 0.5f;
        float z = i - (depth - 1) * 0.5f;
        switch (side)
        {
            case RoomLayout.Side.North: edgeCenter = new Vector3(x, 0, halfD); outward = Vector3.forward; rotY = 180f; break;
            case RoomLayout.Side.South: edgeCenter = new Vector3(x, 0, -halfD); outward = Vector3.back; rotY = 0f; break;
            case RoomLayout.Side.East: edgeCenter = new Vector3(halfW, 0, z); outward = Vector3.right; rotY = 270f; break;
            default: edgeCenter = new Vector3(-halfW, 0, z); outward = Vector3.left; rotY = 90f; break;
        }
    }

    #endregion

    #region Roof

    // Roofs are their own prefabs (Roof_<room>) so they can be hand-tuned once; the builder only
    // creates a roof prefab when it does not exist yet and never overwrites it afterwards.
    private static void BuildRoof(Transform structure, string roomName, int width, int depth, string roofNode, float wallTop)
    {
        string roofPath = OutputDir + "Roof_" + roomName.Replace("Room_", "") + ".prefab";
        var roofPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(roofPath);
        if (roofPrefab == null)
        {
            var generated = new GameObject("Roof");
            try
            {
                if (!GenerateRoof(generated.transform, width, depth, roofNode, wallTop)) return;
                roofPrefab = PrefabUtility.SaveAsPrefabAsset(generated, roofPath);
            }
            finally
            {
                Object.DestroyImmediate(generated);
            }
        }

        var roof = (GameObject)PrefabUtility.InstantiatePrefab(roofPrefab, structure);
        roof.name = "Roof";
    }

    private static bool GenerateRoof(Transform root, int width, int depth, string roofNode, float wallTop)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(RoofModelPath);
        var source = model.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == roofNode);
        if (source == null)
        {
            Debug.LogWarning($"RoomPrefabBuilder: roof node {roofNode} not found in {RoofModelPath}");
            return false;
        }

        // Mesh lives on a child so the prefab root stays at the room origin and the mesh can be tuned freely.
        var roof = new GameObject("RoofMesh");
        roof.transform.SetParent(root, false);
        roof.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        roof.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;

        // Roof meshes are long along X; turn them when the room is deeper than wide.
        Vector3 meshSize = source.sharedMesh.bounds.size;
        bool turn = depth > width && meshSize.x > meshSize.z;
        roof.transform.localRotation = Quaternion.Euler(0f, turn ? 90f : 0f, 0f);

        // Scale so the roof covers the outer walls plus overhang along both axes.
        float outerLong = Mathf.Max(width, depth) + OuterOffset * 2f + 0.1f;
        float outerShort = Mathf.Min(width, depth) + OuterOffset * 2f + 0.1f;
        float meshLong = Mathf.Max(meshSize.x, meshSize.z);
        float meshShort = Mathf.Min(meshSize.x, meshSize.z);
        float scale = Mathf.Max((outerLong + RoofOverhang) / meshLong, (outerShort + RoofOverhang) / meshShort);
        roof.transform.localScale = Vector3.one * scale;

        // The meshes carry decorations hanging below the eaves, so plain bounds are useless for alignment.
        // Use the roof surface instead: eave = lowest vertex on the outer edge, footprint = vertices above it.
        GetRoofFrame(source.sharedMesh, roof.transform, out Vector3 footprintCenter, out float eaveY);
        roof.transform.localPosition += new Vector3(-footprintCenter.x, wallTop - 0.02f - eaveY, -footprintCenter.z);
        return true;
    }

    private static void GetRoofFrame(Mesh mesh, Transform roof, out Vector3 footprintCenter, out float eaveY)
    {
        Matrix4x4 m = Matrix4x4.TRS(roof.localPosition, roof.localRotation, roof.localScale);
        Vector3[] verts = mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();

        float minX = verts.Min(v => v.x), maxX = verts.Max(v => v.x);
        float minZ = verts.Min(v => v.z), maxZ = verts.Max(v => v.z);
        float edgeX = (maxX - minX) * 0.05f, edgeZ = (maxZ - minZ) * 0.05f;

        var edge = verts.Where(v => v.x < minX + edgeX || v.x > maxX - edgeX || v.z < minZ + edgeZ || v.z > maxZ - edgeZ).ToList();
        eaveY = edge.Count > 0 ? edge.Min(v => v.y) : verts.Min(v => v.y);

        float cut = eaveY - 0.01f;
        var surface = verts.Where(v => v.y >= cut).ToList();
        footprintCenter = new Vector3((surface.Min(v => v.x) + surface.Max(v => v.x)) * 0.5f, 0f, (surface.Min(v => v.z) + surface.Max(v => v.z)) * 0.5f);
    }

    #endregion

    #region Canvas

    private static TextMeshProUGUI BuildCanvas(Transform root)
    {
        var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoomCanvasSourcePath));
        PrefabUtility.UnpackPrefabInstance(source, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var canvas = source.transform.Find("Canvas_Room");
        TextMeshProUGUI text = null;
        if (canvas != null)
        {
            var localPos = canvas.localPosition;
            var localRot = canvas.localRotation;
            canvas.SetParent(root, false);
            canvas.localPosition = new Vector3(0f, localPos.y, 0f);
            canvas.localRotation = localRot;
            text = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        Object.DestroyImmediate(source);
        return text;
    }

    #endregion

    #region Helpers

    private static GameObject Load(string partName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(PartsDir + partName + ".prefab");
    }

    // Instantiates a prefab under parent so the anchor geometry is centered on target (relative to space)
    // with its bottom on target.y. anchorChild picks a renderer by name, otherwise the biggest panel is used.
    private static GameObject Place(GameObject prefab, Transform parent, Vector3 target, float rotY, string anchorChild, Transform space = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

        Transform frame = space != null ? space : parent;
        Bounds b = GetBounds(go.transform, anchorChild, frame);
        Vector3 offset = new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
        go.transform.position += frame.TransformVector(offset);
        return go;
    }

    // Bounds of the anchor geometry in the given frame (defaults to the object's parent).
    private static Bounds GetBounds(Transform t, string anchorChild, Transform frame = null)
    {
        frame = frame != null ? frame : t.parent;
        var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => r.bounds.size.sqrMagnitude > 0.0001f).ToList();
        if (renderers.Count == 0) return new Bounds(frame.InverseTransformPoint(t.position), Vector3.zero);

        Renderer anchor = null;
        if (!string.IsNullOrEmpty(anchorChild))
            anchor = renderers.FirstOrDefault(r => r.transform.IsChildOf(t.Find(anchorChild) ?? t) && t.Find(anchorChild) != null);
        if (anchor == null && renderers.Count > 1)
            anchor = renderers.OrderByDescending(r => r.bounds.size.y * Mathf.Max(r.bounds.size.x, r.bounds.size.z)).First();

        var list = anchor != null ? new[] { anchor } : renderers.ToArray();
        Bounds result = default;
        bool init = false;
        foreach (var r in list)
        {
            foreach (var corner in Corners(r.bounds))
            {
                Vector3 p = frame.InverseTransformPoint(corner);
                if (!init) { result = new Bounds(p, Vector3.zero); init = true; }
                else result.Encapsulate(p);
            }
        }
        return result;
    }

    private static Vector3[] Corners(Bounds b)
    {
        Vector3 min = b.min, max = b.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
        };
    }

    #endregion
}
