using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds room prefabs from the artist's house pack (Assets/Art/PANDANIN/ev parçaları 3) with every part at scale 1.
// Output: Room_3x3_Pack, Room_3x3_B_Pack, Room_3x6_Pack, used by levels that set housePack in LevelHouseSetup.
// Tools > NailIt > Build Room Prefabs (House Pack). The old RoomPrefabBuilder prefabs are not touched.
//
// Pack conventions (HousePack_Spec.md): 1 m wall slots, walls 2.5 m high and 0.10 m thick, outside face = local +Z,
// bottom-centre pivots, roofs pivot at wall top on the footprint centre with the ridge along local X.
public static class HousePackRoomBuilder
{
    private const string PackDir = "Assets/Art/PANDANIN/ev parçaları 3/";
    private const string MaterialDir = "Assets/Materials/HousePack/";
    private const string OutputDir = "Assets/Prefabs/Rooms/";
    private const float WallHeight = 2.5f;

    [MenuItem("Tools/NailIt/Build Room Prefabs (House Pack)")]
    public static void BuildAll()
    {
        if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);
        var materials = EnsureMaterials();

        Build("Room_3x3_Pack", 3, 3, materials);
        Build("Room_3x3_B_Pack", 3, 3, materials);
        Build("Room_3x6_Pack", 3, 6, materials);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        foreach (var layout in Object.FindObjectsByType<RoomLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None)) layout.Apply();
    }

    private static void Build(string prefabName, int width, int depth, Dictionary<string, Material> materials)
    {
        var root = new GameObject(prefabName);
        try
        {
            var roomController = root.AddComponent<RoomController>();
            var layout = root.AddComponent<RoomLayout>();
            layout.SetSize(width, depth);
            var recipe = root.AddComponent<RoomMaterialRecipe>();
            recipe.SetSize(width, depth);

            GameObject[] tiles = RoomPrefabBuilder.BuildTiles(root.transform, roomController, width, depth);

            var structure = new GameObject("Structure").transform;
            structure.SetParent(root.transform, false);
            BuildSlots(structure, width, depth);
            BuildCorners(structure, width, depth);
            BuildRoof(structure, "Roof", width, depth, false);
            BuildRoof(structure, "RoofNoEave", width, depth, true);
            ApplyMaterials(structure, materials);

            var roomText = RoomPrefabBuilder.BuildCanvas(root.transform);

            var so = new SerializedObject(roomController);
            so.FindProperty("roomText").objectReferenceValue = roomText;
            so.FindProperty("wallStructure").objectReferenceValue = structure.gameObject;
            var tileProp = so.FindProperty("availableTiles");
            tileProp.arraySize = tiles.Length;
            for (int i = 0; i < tiles.Length; i++) tileProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            layout.Apply();

            PrefabUtility.SaveAsPrefabAsset(root, OutputDir + prefabName + ".prefab");
            Debug.Log($"HousePackRoomBuilder: built {prefabName} ({tiles.Length} tiles)");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    #region Walls and doors

    private static void BuildSlots(Transform structure, int width, int depth)
    {
        var slots = new GameObject("Slots").transform;
        slots.SetParent(structure, false);

        foreach (RoomLayout.Side side in System.Enum.GetValues(typeof(RoomLayout.Side)))
        {
            int count = side == RoomLayout.Side.North || side == RoomLayout.Side.South ? width : depth;
            for (int i = 0; i < count; i++)
            {
                RoomPrefabBuilder.GetSlotFrame(side, i, width, depth, out Vector3 edgeCenter, out _, out float rotY);

                // Slot sits on the tile edge (= wall centre line) and is turned so local +Z points out of the room,
                // which is the pack's outside face. Parts are then placed at the slot origin, unrotated, unscaled.
                var slot = new GameObject(side + "_" + i).transform;
                slot.SetParent(slots, false);
                slot.localPosition = edgeCenter;
                slot.localRotation = Quaternion.Euler(0f, rotY + 180f, 0f);

                var wall = Part("Wall_Plain", slot, "Wall");
                AddBox(wall, new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(1f, WallHeight, 0.1f));

                var window = Part("Wall_Window", slot, "Window");
                AddBox(window, new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(1f, WallHeight, 0.1f));

                // Inner door: open frame the player walks through (same as the old Door_Inner).
                var doorInner = new GameObject("DoorInner").transform;
                doorInner.SetParent(slot, false);
                Part("Wall_DoorLintel_1m", doorInner);
                Part("Door_Frame_A", doorInner);

                // Front door: frame + a leaf that swings on a hinge (same idea as the old Door_Outher).
                var doorOuter = new GameObject("DoorOuter").transform;
                doorOuter.SetParent(slot, false);
                Part("Wall_DoorLintel_1m", doorOuter);
                Part("Door_Frame_A", doorOuter);
                BuildDoorLeaf(doorOuter);
            }
        }
    }

    private static void BuildDoorLeaf(Transform parent)
    {
        var leaf = new GameObject("Leaf");
        leaf.transform.SetParent(parent, false);
        Part("Door_Panel_C", leaf.transform);

        var body = leaf.AddComponent<Rigidbody>();
        body.mass = 10f;
        body.angularDamping = 2f;

        var box = leaf.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.975f, 0f);
        box.size = new Vector3(0.88f, 1.9f, 0.07f); // narrower than the leaf so it never rubs the neighbouring wall colliders

        var hinge = leaf.AddComponent<HingeJoint>();
        hinge.anchor = new Vector3(-0.45f, 0.975f, 0f); // inside edge of the frame post
        hinge.axis = Vector3.up;
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -100f, max = 100f };
    }

    // One Wall_CornerTrim per room corner, centred on the corner of the wall centre lines (fills the 5 cm notch).
    private static void BuildCorners(Transform structure, int width, int depth)
    {
        var corners = new GameObject("Corners").transform;
        corners.SetParent(structure, false);
        float hx = width * 0.5f, hz = depth * 0.5f;
        Part("Wall_CornerTrim", corners, "NE").transform.localPosition = new Vector3(hx, 0f, hz);
        Part("Wall_CornerTrim", corners, "NW").transform.localPosition = new Vector3(-hx, 0f, hz);
        Part("Wall_CornerTrim", corners, "SE").transform.localPosition = new Vector3(hx, 0f, -hz);
        Part("Wall_CornerTrim", corners, "SW").transform.localPosition = new Vector3(-hx, 0f, -hz);
    }

    #endregion

    #region Roof

    // One-piece roof at wall top, room centre. Ridges run along the longer side and along Z on square rooms
    // (same rule as the old builder), so neighbouring rooms form clean valleys.
    private static void BuildRoof(Transform structure, string name, int width, int depth, bool noEave)
    {
        int alongRidge = Mathf.Max(width, depth), across = Mathf.Min(width, depth);
        string model = $"Roof_Gable_{across}x{alongRidge}" + (noEave ? "_NoEave" : "");
        var roof = Part(model, structure, name);
        roof.transform.localPosition = new Vector3(0f, WallHeight, 0f);
        bool ridgeAlongZ = depth >= width;
        roof.transform.localRotation = Quaternion.Euler(0f, ridgeAlongZ ? 90f : 0f, 0f);
        roof.AddComponent<DitherOccluder>(); // no collider, so the camera dither needs this to fade it over the player
        if (noEave) roof.SetActive(false);
    }

    #endregion

    #region Materials

    // The pack ships its FBX materials embedded; these project materials make the texture setup explicit
    // (wrap repeat, transparent glass) and are matched to the FBX slots by name.
    private static Dictionary<string, Material> EnsureMaterials()
    {
        if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);
        return new Dictionary<string, Material>
        {
            { "Ev_Atlas", EnsureMaterial("Ev_Atlas", "Ev_Texture_Atlas.png", false, new Color(0.9f, 0.86f, 0.85f)) },
            { "Ev_Ahsap", EnsureMaterial("Ev_Ahsap", "Ev_Ahsap_Tekrar.png", false, new Color(0.82f, 0.76f, 0.76f)) },
            { "Ev_Sindil", EnsureMaterial("Ev_Sindil", "Ev_Sindil_Tekrar.png", false, new Color(0.78f, 0.71f, 0.72f)) },
            { "Glass", EnsureMaterial("Glass", null, true, Color.white) },
        };
    }

    // Opaque parts use the environment's NailIt/WarmStylized look (same paint noise, shadow fill and rim as
    // the generated warm materials). The tint mutes the pack's saturated orange towards the scene palette.
    // Every level's StyleTestRuntime registers the shader's dither variant, so roofs still fade over the player.
    // Glass stays URP Lit: the warm shader is opaque only.
    private static Material EnsureMaterial(string name, string texture, bool glass, Color tint)
    {
        string path = MaterialDir + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var warm = Shader.Find("NailIt/WarmStylized");
        mat = new Material(glass || warm == null ? Shader.Find("Universal Render Pipeline/Lit") : warm);
        if (texture != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PackDir + texture));
        if (!glass && warm != null)
        {
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_PaintNoise", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StyleTest_Warm/Generated/Textures/PaintNoise.png"));
            mat.SetFloat("_PaintStrength", 0.42f);
            mat.SetFloat("_PaintScale", 0.55f);
            mat.SetColor("_ShadowTint", new Color(0.27f, 0.19f, 0.36f));
            mat.SetColor("_RimColor", new Color(0.2f, 0.11f, 0.05f));
            mat.enableInstancing = true;
        }
        mat.SetFloat("_Smoothness", 0.1f);
        if (glass)
        {
            mat.SetColor("_BaseColor", new Color(0.72f, 0.88f, 1f, 0.35f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.8f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void ApplyMaterials(Transform root, Dictionary<string, Material> materials)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                foreach (var kv in materials)
                {
                    if (!mats[i].name.StartsWith(kv.Key)) continue;
                    mats[i] = kv.Value;
                    changed = true;
                    break;
                }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    #endregion

    #region Helpers

    // Instantiates a pack model under parent at the parent origin, scale 1.
    private static GameObject Part(string model, Transform parent, string name = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PackDir + model + ".fbx");
        if (asset == null) throw new System.Exception("HousePackRoomBuilder: missing " + PackDir + model + ".fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        if (name != null) go.name = name;
        return go;
    }

    private static void AddBox(GameObject go, Vector3 center, Vector3 size)
    {
        var box = go.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }

    #endregion
}
