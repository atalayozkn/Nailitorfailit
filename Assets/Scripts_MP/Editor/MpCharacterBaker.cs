#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds Resources/MpCharacterSet_MP.asset: the extra character models (static meshes in T-pose) are re-skinned to the
// Mixamo skeleton of the player model ("karakter 1 erkek") so they run the same animations, ragdoll and bone sync.
//
//  1. Each model is scaled uniformly to the height of the player model, feet on the ground, centred on the skeleton.
//  2. Every vertex is bound to the bones whose skeleton segments (bone -> child bone) are nearest, blended softly
//     (up to 4 bones) so joints bend instead of tearing.
//  3. The new mesh uses the player model's bind poses and bone order, so it plugs into the same SkinnedMeshRenderer.
public static class MpCharacterBaker
{
    private const string BaseModel = "Assets/Art/PANDANIN/karakterler/karakter 1 erkek.fbx";
    private const string OutputDir = "Assets/Scripts_MP/Resources";
    private const string OutputAsset = OutputDir + "/MpCharacterSet_MP.asset";

    private static readonly (string name, string path)[] Extras =
    {
        ("Woman", "Assets/Art/PANDANIN/karakterler/karakter 2 kadın.fbx"),
        ("Bruiser", "Assets/Art/PANDANIN/karakterler/karakter 3 kabadayı.fbx"),
        ("Plant Lady", "Assets/Art/PANDANIN/karakterler/karakter 4 bitki kadın.fbx"),
    };

    // How quickly a bone's influence fades with distance (metres) beyond the nearest bone.
    private const float Softness = 0.03f;
    private const float MinWeight = 0.06f;

    public static void Bake()
    {
        var baseFbx = AssetDatabase.LoadAssetAtPath<GameObject>(BaseModel);
        if (baseFbx == null) { Debug.LogError("[MP-CHAR] base model missing: " + BaseModel); return; }

        GameObject baseInstance = Object.Instantiate(baseFbx);
        try
        {
            SkinnedMeshRenderer baseRenderer = baseInstance.GetComponentInChildren<SkinnedMeshRenderer>();
            Mesh baseMesh = baseRenderer.sharedMesh;
            Transform[] bones = baseRenderer.bones;
            Matrix4x4[] bindposes = baseMesh.bindposes;

            // Skeleton segments in MESH space. The mesh space is not the renderer's world space (the importer bakes an
            // offset into it), so bone positions come from the bind poses: bindpose[i] maps mesh -> bone i local.
            var segments = new List<(int bone, Vector3 a, Vector3 b)>();
            for (int i = 0; i < bones.Length; i++)
            {
                Matrix4x4 boneToMesh = bindposes[i].inverse;
                Vector3 a = boneToMesh.MultiplyPoint3x4(Vector3.zero);
                foreach (Transform child in bones[i])
                {
                    if (!child.name.StartsWith("mixamorig")) continue;
                    segments.Add((i, a, boneToMesh.MultiplyPoint3x4(child.localPosition)));
                }
            }

            Bounds baseBounds = baseMesh.bounds;
            float targetBottom = baseBounds.min.y, targetTop = baseBounds.max.y;
            Debug.Log($"[MP-CHAR] base mesh y {targetBottom:F3}..{targetTop:F3}, {bones.Length} bones, {segments.Count} segments");

            if (!AssetDatabase.IsValidFolder(OutputDir)) AssetDatabase.CreateFolder("Assets/Scripts_MP", "Resources");
            AssetDatabase.DeleteAsset(OutputAsset);

            var set = ScriptableObject.CreateInstance<MpCharacterSet_MP>();
            var entries = new List<MpCharacterSet_MP.Entry> { new MpCharacterSet_MP.Entry { displayName = "Man" } };
            AssetDatabase.CreateAsset(set, OutputAsset);

            foreach (var (name, path) in Extras)
            {
                Mesh mesh = BuildMesh(name, path, baseMesh, bindposes, segments, bones.Length, targetBottom, targetTop, out Material material);
                if (mesh == null) continue;

                AssetDatabase.AddObjectToAsset(mesh, set);
                entries.Add(new MpCharacterSet_MP.Entry { displayName = name, mesh = mesh, material = material });
            }

            set.entries = entries.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MP-CHAR] baked {entries.Count - 1} extra characters -> {OutputAsset}");
        }
        finally
        {
            Object.DestroyImmediate(baseInstance);
        }
    }

    private static Mesh BuildMesh(string name, string path, Mesh baseMesh, Matrix4x4[] bindposes,
                                  List<(int bone, Vector3 a, Vector3 b)> segments, int boneCount,
                                  float targetBottom, float targetTop, out Material material)
    {
        material = null;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (fbx == null) { Debug.LogError("[MP-CHAR] missing " + path); return null; }

        GameObject instance = Object.Instantiate(fbx);
        try
        {
            // Merge every renderer's geometry into one mesh in model space.
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh source = filter != null ? filter.sharedMesh : null;
                if (source == null || source.vertexCount == 0) continue;

                int offset = vertices.Count;
                Vector3[] v = source.vertices;
                Vector3[] n = source.normals;
                Vector2[] uv = source.uv;
                for (int i = 0; i < v.Length; i++)
                {
                    vertices.Add(renderer.transform.TransformPoint(v[i]));
                    normals.Add(n.Length == v.Length ? renderer.transform.TransformDirection(n[i]).normalized : Vector3.up);
                    uvs.Add(uv.Length == v.Length ? uv[i] : Vector2.zero);
                }

                for (int sub = 0; sub < source.subMeshCount; sub++)
                    foreach (int t in source.GetTriangles(sub)) triangles.Add(t + offset);

                if (material == null) material = renderer.sharedMaterial;
            }

            if (vertices.Count == 0) { Debug.LogError("[MP-CHAR] no geometry in " + path); return null; }

            // Uniform scale to the player's height, feet on the ground, centred on the skeleton (x = 0; z by the torso).
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 v in vertices) { minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); }

            float scale = (targetTop - targetBottom) / (maxY - minY);

            float minX = float.MaxValue, maxX = float.MinValue, torsoZ = 0f;
            int torsoCount = 0;
            foreach (Vector3 v in vertices)
            {
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                float h = (v.y - minY) / (maxY - minY);
                if (h > 0.35f && h < 0.65f && Mathf.Abs(v.x) < 0.25f / scale) { torsoZ += v.z; torsoCount++; }
            }

            float centreX = (minX + maxX) * 0.5f;
            float centreZ = torsoCount > 0 ? torsoZ / torsoCount : 0f;
            float skeletonZ = segments[0].a.z;

            var finalVertices = new Vector3[vertices.Count];
            for (int i = 0; i < finalVertices.Length; i++)
            {
                Vector3 v = vertices[i];
                finalVertices[i] = new Vector3((v.x - centreX) * scale,
                                               targetBottom + (v.y - minY) * scale,
                                               skeletonZ + (v.z - centreZ) * scale);
            }

            // Skin weights from the nearest skeleton segments.
            var weights = new BoneWeight[finalVertices.Length];
            var best = new float[boneCount];
            for (int i = 0; i < finalVertices.Length; i++)
            {
                for (int b = 0; b < boneCount; b++) best[b] = float.MaxValue;
                foreach (var (bone, a, b) in segments)
                {
                    float d = DistanceToSegment(finalVertices[i], a, b);
                    if (d < best[bone]) best[bone] = d;
                }

                weights[i] = PickWeights(best);
            }

            var mesh = new Mesh { name = "MpCharacter_" + name.Replace(' ', '_') };
            if (finalVertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = finalVertices;
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.boneWeights = weights;
            mesh.bindposes = bindposes;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            Debug.Log($"[MP-CHAR] {name}: {finalVertices.Length} verts, scale {scale:F3}, bounds {mesh.bounds}, material {(material != null ? material.name : "null")}");
            return mesh;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static BoneWeight PickWeights(float[] distance)
    {
        // The 4 nearest bones.
        int i0 = -1, i1 = -1, i2 = -1, i3 = -1;
        for (int round = 0; round < 4; round++)
        {
            int pick = -1;
            for (int b = 0; b < distance.Length; b++)
            {
                if (b == i0 || b == i1 || b == i2 || b == i3) continue;
                if (pick < 0 || distance[b] < distance[pick]) pick = b;
            }

            if (round == 0) i0 = pick; else if (round == 1) i1 = pick; else if (round == 2) i2 = pick; else i3 = pick;
        }

        float d0 = distance[i0];
        float w0 = 1f;
        float w1 = Mathf.Exp(-(distance[i1] - d0) / Softness);
        float w2 = Mathf.Exp(-(distance[i2] - d0) / Softness);
        float w3 = Mathf.Exp(-(distance[i3] - d0) / Softness);
        float sum = w0 + w1 + w2 + w3;
        w0 /= sum; w1 /= sum; w2 /= sum; w3 /= sum;
        if (w1 < MinWeight) w1 = 0f;
        if (w2 < MinWeight) w2 = 0f;
        if (w3 < MinWeight) w3 = 0f;
        sum = w0 + w1 + w2 + w3;

        return new BoneWeight
        {
            boneIndex0 = i0, weight0 = w0 / sum,
            boneIndex1 = i1, weight1 = w1 / sum,
            boneIndex2 = i2, weight2 = w2 / sum,
            boneIndex3 = i3, weight3 = w3 / sum,
        };
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float length = ab.sqrMagnitude;
        float t = length < 1e-8f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / length);
        return Vector3.Distance(p, a + ab * t);
    }
}
#endif
