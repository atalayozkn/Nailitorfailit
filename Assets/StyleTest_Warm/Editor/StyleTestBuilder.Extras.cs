using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Terrain trees, lamp glow, stand-in dressing, the dog and the clean rebuild.
public static partial class StyleTestBuilder
{
    static void ReplaceTerrainTrees(TerrainData data, Material foliage)
    {
        var holder = AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(Gen + "/FoliageMeshes.asset");
        var protos = data.treePrototypes;
        for (int i = 0; i < protos.Length; i++)
        {
            string path = Gen + "/Tree_Terrain_" + i + ".prefab";
            if (AssetDatabase.GetAssetPath(protos[i].prefab) == path) continue;

            var old = Object.Instantiate(protos[i].prefab);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in old.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            Object.DestroyImmediate(old);

            float h = Mathf.Max(bounds.size.y * 0.8f, 1f);
            var fit = new Bounds(new Vector3(0, h * 0.5f, 0), new Vector3(h * 0.72f, h, h * 0.72f));
            var mesh = MakeFoliage(fit, true, -1f, 4242 + i);
            mesh.name = "Tree_Terrain_" + i;
            AssetDatabase.AddObjectToAsset(mesh, holder);

            var go = new GameObject(mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = foliage;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = h * 0.5f;
            capsule.center = new Vector3(0, h * 0.25f, 0); go.AddComponent<LODGroup>().SetLODs(new[] { new LOD(0.01f, new Renderer[] { go.GetComponent<MeshRenderer>() }) });
            protos[i].prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            protos[i].bendFactor = 0;
            Object.DestroyImmediate(go);
        }
        data.treePrototypes = protos;
        data.RefreshPrototypes();
    }

    static void ApplyLampGlow(Shader surface, Texture2D noise)
    {
        var glow = NewMat("LampGlow", surface, m =>
        {
            m.SetColor("_BaseColor", new Color(1f, 0.66f, 0.3f));
            SetStyle(m, Tex("PaintNoise"), 0f);
            m.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            m.SetColor("_EmissionColor", new Color(0.9f, 0.42f, 0.1f));
            m.EnableKeyword("_EMISSION");
        });
        var iron = NewMat("LampIron", surface, m =>
        {
            m.SetColor("_BaseColor", new Color(0.2f, 0.16f, 0.17f));
            SetStyle(m, Tex("PaintNoise"), 0.2f);
        });

        var holder = AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(Gen + "/FoliageMeshes.asset");
        var cache = new Dictionary<string, Mesh>();
        foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (f.sharedMesh == null || !AssetDatabase.GetAssetPath(f.sharedMesh).Contains("sokak lambası")) continue;

            // Hide the bent-arm lamp and stand an upright lantern post in its place (the source objects are rotated,
            // so the new one is its own unrotated child). The old collider stays.
            var r = f.GetComponent<MeshRenderer>();
            var bounds = r.bounds;
            float height = bounds.size.y;
            string key = $"Lamp_{height:0.00}";
            if (!cache.TryGetValue(key, out var mesh))
            {
                mesh = MakeLampPost(height, Vector3.one);
                mesh.name = key;
                AssetDatabase.AddObjectToAsset(mesh, holder);
                cache[key] = mesh;
            }
            r.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            var post = new GameObject("StyleTest_LampPost");
            post.transform.SetParent(f.transform, true);
            post.transform.SetPositionAndRotation(new Vector3(f.transform.position.x, bounds.min.y, f.transform.position.z), Quaternion.identity);
            var ls = post.transform.lossyScale;
            post.transform.localScale = new Vector3(post.transform.localScale.x / ls.x, post.transform.localScale.y / ls.y, post.transform.localScale.z / ls.z);
            post.AddComponent<MeshFilter>().sharedMesh = mesh;
            post.AddComponent<MeshRenderer>().sharedMaterials = new[] { iron, glow };
        }
    }

    // Lantern street lamp, about 300 tris. Built in world units, then divided by the instance scale.
    // Submesh 0 = iron, submesh 1 = glowing glass.
    static Mesh MakeLampPost(float h, Vector3 scale)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var iron = new List<int>();
        var glass = new List<int>();

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> into)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            int i = verts.Count;
            verts.AddRange(new[] { a, b, c, d });
            norms.AddRange(new[] { n, n, n, n });
            into.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        void Ring(float r0, float r1, float y0, float y1, int sides, List<int> into, float turn = 0f)
        {
            for (int k = 0; k < sides; k++)
            {
                float a0 = turn + k * Mathf.PI * 2 / sides, a1 = turn + (k + 1) * Mathf.PI * 2 / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                int i = verts.Count;
                verts.AddRange(new[] { d0 * r0 + Vector3.up * y0, d0 * r1 + Vector3.up * y1, d1 * r1 + Vector3.up * y1, d1 * r0 + Vector3.up * y0 });
                norms.AddRange(new[] { d0, d0, d1, d1 });
                into.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }
        void Box(Vector3 c, Vector3 e, List<int> into)
        {
            Vector3 X = Vector3.right * e.x, Y = Vector3.up * e.y, Z = Vector3.forward * e.z;
            Quad(c + Y - X - Z, c + Y - X + Z, c + Y + X + Z, c + Y + X - Z, into);
            Quad(c - Y - X + Z, c - Y - X - Z, c - Y + X - Z, c - Y + X + Z, into);
            Quad(c - Z - X - Y, c - Z - X + Y, c - Z + X + Y, c - Z + X - Y, into);
            Quad(c + Z + X - Y, c + Z + X + Y, c + Z - X + Y, c + Z - X - Y, into);
            Quad(c - X - Z + Y, c - X - Z - Y, c - X + Z - Y, c - X + Z + Y, into);
            Quad(c + X + Z + Y, c + X + Z - Y, c + X - Z - Y, c + X - Z + Y, into);
        }

        float lantern = Mathf.Clamp(h * 0.12f, 0.32f, 0.5f);
        float top = h - lantern * 0.55f;
        Ring(0.16f, 0.12f, 0f, 0.3f, 8, iron);
        Ring(0.065f, 0.055f, 0.3f, top - lantern - 0.08f, 8, iron);
        Ring(0.12f, 0.09f, top - lantern - 0.1f, top - lantern, 8, iron);
        Box(new Vector3(0, top - lantern * 0.5f, 0), new Vector3(lantern * 0.42f, lantern * 0.5f, lantern * 0.42f), glass);
        float w = lantern * 0.47f;
        foreach (var c in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
            Box(new Vector3(c.x * w, top - lantern * 0.5f, c.y * w), new Vector3(0.025f, lantern * 0.5f, 0.025f), iron);
        Box(new Vector3(0, top - lantern - 0.015f, 0), new Vector3(w + 0.03f, 0.03f, w + 0.03f), iron);
        // Square roof, turned 45 degrees so its edges line up with the lantern box.
        Ring((w + 0.06f) * 1.4142f, 0.02f, top, h, 4, iron, Mathf.PI * 0.25f);

        var inv = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
        var mesh = new Mesh { subMeshCount = 2 };
        mesh.SetVertices(verts.Select(v => Vector3.Scale(v, inv)).ToList());
        mesh.SetNormals(norms.Select(n => Vector3.Scale(n, scale).normalized).ToList());
        mesh.SetUVs(0, verts.Select(_ => Vector2.zero).ToList());
        mesh.SetTriangles(iron, 0);
        mesh.SetTriangles(glass, 1);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Flowers dotted over the grass as one combined mesh: one draw call, no shadows, no colliders.
    static void AddFlowerScatter(Material foliage)
    {
        var terrain = Object.FindFirstObjectByType<Terrain>();
        var data = terrain.terrainData;
        var origin = terrain.transform.position;
        var splat = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        Physics.SyncTransforms();

        var rng = new System.Random(7);
        float R() => (float)rng.NextDouble();
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var winds = new List<Vector2>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        Vector3[] octa = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        int[] octaTris = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };

        int placed = 0;
        for (int attempt = 0; attempt < 6000 && placed < 700; attempt++)
        {
            var p = new Vector3(origin.x + R() * data.size.x, 0, origin.z + R() * data.size.z);
            int ax = Mathf.Clamp((int)((p.x - origin.x) / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int az = Mathf.Clamp((int)((p.z - origin.z) / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            if (splat[az, ax, 1] < 0.7f) continue;
            if (Fbm(p.x / data.size.x, p.z / data.size.z, 18, 2, 91) < 0.48f) continue;

            // Skip anything not on open ground (sidewalks, houses, the build site).
            if (!Physics.Raycast(p + Vector3.up * 50, Vector3.down, out var hit, 100f) || hit.collider is not TerrainCollider) continue;
            p.y = hit.point.y;

            float u = R() < 0.6f ? UFlower : UYellow;
            int blooms = 2 + rng.Next(3);
            for (int b = 0; b < blooms; b++)
            {
                var c = p + new Vector3((R() - 0.5f) * 0.6f, 0.1f + R() * 0.08f, (R() - 0.5f) * 0.45f);
                float size = 0.08f + R() * 0.06f;
                int s = verts.Count;
                foreach (var v in octa)
                {
                    verts.Add(c + Vector3.Scale(v, new Vector3(size, size * 0.6f, size)) - origin);
                    norms.Add((v + Vector3.up).normalized);
                    uvs.Add(new Vector2(u, 0.5f));
                    winds.Add(new Vector2(1, 0));
                }
                foreach (int t in octaTris) tris.Add(s + t);
            }
            placed++;
        }

        var mesh = new Mesh { name = "FlowerScatter" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, winds);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(mesh, AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(Gen + "/FoliageMeshes.asset"));

        var go = new GameObject("StyleTest_Flowers");
        go.transform.position = origin;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = foliage;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Small stand-in dressing, so the test shows how much density adds.
    static Mesh MakeBoxy(string name, Vector3 size, bool plants, int seed)
    {
        var winds = new List<Vector2>();
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        void Face(Vector3 c, Vector3 u, Vector3 v, float shade)
        {
            int s = verts.Count;
            var n = Vector3.Cross(u, v).normalized;
            verts.AddRange(new[] { c - u - v, c + u - v, c + u + v, c - u + v });
            norms.AddRange(new[] { n, n, n, n });
            uvs.AddRange(new[] { new Vector2(UTrunk, shade), new Vector2(UTrunk, shade), new Vector2(UTrunk, 0.95f), new Vector2(UTrunk, 0.95f) });
            tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        var e = size * 0.5f;
        var c0 = new Vector3(0, e.y, 0);
        Face(c0 + Vector3.up * e.y, Vector3.forward * e.z, Vector3.right * e.x, 0.85f);
        Face(c0 + Vector3.right * e.x, Vector3.up * e.y, Vector3.forward * e.z, 0.3f);
        Face(c0 - Vector3.right * e.x, Vector3.forward * e.z, Vector3.up * e.y, 0.3f);
        Face(c0 + Vector3.forward * e.z, Vector3.right * e.x, Vector3.up * e.y, 0.3f);
        Face(c0 - Vector3.forward * e.z, Vector3.up * e.y, Vector3.right * e.x, 0.3f);

        var mesh = new Mesh { name = name };
        if (plants)
        {
            var top = new Bounds(new Vector3(0, size.y + size.z * 0.42f, 0), new Vector3(size.x * 0.98f, size.z * 0.95f, size.z * 0.95f));
            var bush = MakeFoliage(top, false, UFlower, seed);
            int s = verts.Count;
            verts.AddRange(bush.vertices);
            norms.AddRange(bush.normals);
            uvs.AddRange(bush.uv);
            winds.AddRange(Enumerable.Repeat(Vector2.zero, s - winds.Count));
            winds.AddRange(bush.uv2);
            tris.AddRange(bush.triangles.Select(t => t + s));
            Object.DestroyImmediate(bush);
        }
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        winds.AddRange(Enumerable.Repeat(Vector2.zero, verts.Count - winds.Count));
        mesh.SetUVs(1, winds);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddDressing(Material foliage)
    {
        var holder = AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(Gen + "/FoliageMeshes.asset");
        var planterSize = new Vector3(1.5f, 0.42f, 0.55f);
        var crateSize = new Vector3(0.8f, 0.8f, 0.8f);
        var planterMesh = MakeBoxy("Planter", planterSize, true, 901);
        var crateMesh = MakeBoxy("Crate", crateSize, false, 0);
        AssetDatabase.AddObjectToAsset(planterMesh, holder);
        AssetDatabase.AddObjectToAsset(crateMesh, holder);

        var root = new GameObject("StyleTest_Dressing");
        var terrain = Object.FindFirstObjectByType<Terrain>();
        void Place(Mesh m, Vector3 size, string name, float x, float z, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = foliage;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, size.y * 0.5f, 0);
            box.size = size;
        }
        foreach (float x in new[] { -24.2f, -22.5f, -17.2f, -15.5f, -13.8f, -9.5f, -7.8f, -6.1f })
            Place(planterMesh, planterSize, "Planter", x, 6.15f, 0);
        Place(crateMesh, crateSize, "Crate", -13.6f, 12.6f, 12);
        Place(crateMesh, crateSize, "Crate", -12.7f, 12.3f, -8);
        Place(crateMesh, crateSize, "Crate", -13.2f, 11.5f, 30);
    }

    static void AddDog(Dictionary<Material, Material> map)
    {
        const string fbx = Root + "/Dog/golden_kopek_low_poly.fbx";
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        var clips = imp.defaultClipAnimations;
        string[] oneShots = { "|Sit", "|Roll", "|Bark", "|Eat", "|HeadTilt" };
        foreach (var c in clips)
        {
            c.loopTime = !oneShots.Any(s => c.name.EndsWith(s));
            c.loopPose = c.loopTime;
        }
        FlushMaterials();
        imp.clipAnimations = clips;
        imp.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Gen + "/Dog.controller");
        var sm = ctrl.layers[0].stateMachine;
        foreach (var clip in all.OrderBy(c => c.name.EndsWith("|IdleSit") ? 0 : 1))
            sm.AddState(clip.name.Substring(clip.name.IndexOf('|') + 1)).motion = clip;
        AssetDatabase.SaveAssets();

        var dog = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        dog.name = "StyleTest_GoldenDog";
        var terrain = Object.FindFirstObjectByType<Terrain>();
        var pos = new Vector3(-22.4f, 0, 4.3f);
        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        dog.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 200, 0));
        dog.transform.localScale = Vector3.one * 1.35f;
        if (!dog.TryGetComponent<Animator>(out var animator)) animator = dog.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        foreach (var r in dog.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null) { string wanted = mats[i].name; var match = map.FirstOrDefault(p => p.Key.name == wanted); if (match.Value != null) mats[i] = match.Value; } // The fresh FBX import makes its own "atlas" material, so match by name.
            r.sharedMaterials = mats;
        }
    }

    // Imports can unload textures that earlier materials pointed at, so link everything by path once imports are done.
    static int RelinkTextures(Dictionary<Material, Material> map)
    {
        int missing = 0;
        foreach (var pair in map)
        {
            Texture tex = pair.Key.HasProperty("_BaseMap") ? pair.Key.GetTexture("_BaseMap") : null;
            if (tex == null && pair.Key.HasProperty("_MainTex")) tex = pair.Key.GetTexture("_MainTex");
            if (tex == null) continue;

            string source = AssetDatabase.GetAssetPath(tex);
            Texture2D painted = null;
            if (!string.IsNullOrEmpty(source) && source.StartsWith("Assets/"))
            {
                string hash;
                using (var md5 = System.Security.Cryptography.MD5.Create())
                    hash = System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(source))).Replace("-", "").Substring(0, 12);
                painted = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Painted_" + hash + ".png");
            }
            pair.Value.SetTexture("_BaseMap", painted != null ? painted : tex);
            if (painted == null) missing++;
            EditorUtility.SetDirty(pair.Value);
        }

        var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/PaintNoise.png");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m.HasProperty("_PaintNoise")) { m.SetTexture("_PaintNoise", noise); EditorUtility.SetDirty(m); }
        }
        var foliage = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Foliage.mat");
        foliage.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/FoliageRamp.png"));
        EditorUtility.SetDirty(foliage);

        var dirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Gen + "/Layer_Dirt.terrainlayer");
        var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Gen + "/Layer_Grass.terrainlayer");
        dirt.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Ground_Dirt.png");
        grass.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Ground_Grass.png");
        EditorUtility.SetDirty(dirt);
        EditorUtility.SetDirty(grass);
        return missing;
    }

    // Starts over from the untouched Level_01. keepPainted skips the slow texture filter.
    public static string Rebuild(bool keepPainted)
    {
        materialSetup.Clear();
        EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        AssetDatabase.DeleteAsset(ScenePath);
        if (AssetDatabase.IsValidFolder(Gen))
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Gen }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p)) continue;
                if (p.StartsWith(TexDir + "/")) { if (keepPainted || !Path.GetFileName(p).StartsWith("Painted_")) continue; }
                AssetDatabase.DeleteAsset(p);
            }
        }
        paintedByHash.Clear();
        return BuildAll();
    }
}
