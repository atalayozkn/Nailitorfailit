using UnityEditor;
using UnityEngine;

// Adds an invisible 45 degree ramp to the two long edges of the sidewalk prefab's box collider,
// so every sidewalk in every scene gets them. Running it again replaces the previous ramps.
public static class SidewalkRampSetup
{
    const string SidewalkPrefabGuid = "d438576f01e4c3b4e8cdfb7a4d75a10a";
    const string RampObjectName = "Sidewalk Ramps";
    const string MeshAssetPath = "Assets/Prefabs/Props/Yollar/SidewalkRampMeshes.asset";
    const string MaterialPath = "Assets/Prefabs/Props/Yollar/SidewalkRamp.physicMaterial";

    [MenuItem("Tools/Level_01/Add Sidewalk Ramps To Prefab")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[SidewalkRamp] Stop Play mode first.");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(SidewalkPrefabGuid);
        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError("[SidewalkRamp] Sidewalk prefab not found.");
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            GameObject root = scope.prefabContentsRoot;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == RampObjectName)
                    Object.DestroyImmediate(child.gameObject);
            }
            AssetDatabase.DeleteAsset(MeshAssetPath);

            BoxCollider box = root.GetComponentInChildren<BoxCollider>(true);
            if (box == null)
            {
                Debug.LogError("[SidewalkRamp] The sidewalk prefab has no box collider.");
                return;
            }

            if (Vector3.Dot(box.transform.up, Vector3.up) < 0.99f)
            {
                Debug.LogError("[SidewalkRamp] The collider's local up isn't world up, so ramps would be wrong. Nothing changed.");
                return;
            }

            var rampObject = new GameObject(RampObjectName);
            rampObject.layer = box.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(rampObject, GameObjectUtility.GetStaticEditorFlags(box.gameObject));
            rampObject.transform.SetParent(box.transform, false);
            rampObject.AddComponent<SidewalkRamp>();

            Vector3 min = box.center - box.size * 0.5f;
            Vector3 max = box.center + box.size * 0.5f;
            float height = max.y - min.y;

            // Run equals rise in world units for a 45 degree slope, even if the object is scaled.
            Vector3 scale = box.transform.lossyScale;
            float worldRise = height * Mathf.Abs(scale.y);
            float runZ = worldRise / Mathf.Abs(scale.z);

            Mesh[] meshes =
            {
                BuildRamp("Ramp_PosZ", new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), new Vector3(0f, 0f, runZ), height),
                BuildRamp("Ramp_NegZ", new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(0f, 0f, -runZ), height)
            };

            AssetDatabase.CreateAsset(meshes[0], MeshAssetPath);
            for (int i = 1; i < meshes.Length; i++)
                AssetDatabase.AddObjectToAsset(meshes[i], MeshAssetPath);

            // Slides freely while moving, but static friction stops the player drifting while idle.
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
            if (material == null)
            {
                material = new PhysicsMaterial("SidewalkRamp")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0.6f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounciness = 0f
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            AssetDatabase.SaveAssets();

            foreach (Mesh mesh in meshes)
            {
                var meshCollider = rampObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                meshCollider.convex = true;
                meshCollider.sharedMaterial = material;
            }

            Debug.Log($"[SidewalkRamp] Added {meshes.Length} ramps. Box size={box.size}, center={box.center}, world run={worldRise:F2}.");
        }
    }

    // A wedge along the edge from start to end: full height at the sidewalk, zero at the outer edge.
    // The edge direction must wind so the sloped face points up and outward.
    static Mesh BuildRamp(string meshName, Vector3 start, Vector3 end, Vector3 outward, float height)
    {
        Vector3 up = Vector3.up * height;
        var mesh = new Mesh { name = meshName };
        mesh.vertices = new[]
        {
            start, end, end + up, start + up,
            start + outward, end + outward
        };
        mesh.triangles = new[]
        {
            3, 2, 5, 3, 5, 4, // slope
            0, 4, 5, 0, 5, 1, // bottom
            0, 3, 4,          // start end cap
            1, 5, 2,          // far end cap
            0, 1, 2, 0, 2, 3  // back against the sidewalk
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
