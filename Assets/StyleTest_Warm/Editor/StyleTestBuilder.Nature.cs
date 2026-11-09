using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Pond, butterflies, birds, chirps, cloud shadows and pollen motes for the warm style test.
public static partial class StyleTestBuilder
{
    static readonly Vector3 DefaultHutCenter = new Vector3(-19.5f, 0f, 10f);
    // Centre of the yard the ambience gathers around. The level applier points it at each level's house.
    static Vector3 HutCenter = DefaultHutCenter;
    // Set by the level applier to place the pond itself; null searches near the Level_01 hut.
    static Vector3? pondCenter;
    // Set by the level applier so each scene keeps its own generated meshes.
    static string meshHolderOverride;
    static string MeshHolderPath => meshHolderOverride ?? Gen + "/FoliageMeshes.asset";
    const float PondRadius = 2.6f;

    // Material settings, re-applied by path once every import has finished. Imports during the
    // build can reload material assets and drop edits made before them.
    static readonly Dictionary<string, System.Action<Material>> materialSetup = new();

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + name + ".png");

    static Material Configure(string path, Shader shader, System.Action<Material> setup)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.enableInstancing = true;
        setup?.Invoke(mat);
        materialSetup[path] = setup;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material NewMat(string name, Shader shader, System.Action<Material> setup = null) =>
        Configure(MatDir + "/" + name + ".mat", shader, setup);

    static void ReapplyMaterialSetup()
    {
        foreach (var pair in materialSetup)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(pair.Key);
            if (mat == null) continue;
            pair.Value?.Invoke(mat);
            EditorUtility.SetDirty(mat);
        }
    }

    static void AddMesh(Mesh mesh) =>
        AssetDatabase.AddObjectToAsset(mesh, AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(MeshHolderPath));

    static Mesh BuildMesh(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<Vector2> wind, List<int> t)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, wind);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        AddMesh(mesh);
        return mesh;
    }

    // ---------- Pond ----------

    static Vector3 FindPondSpot(Terrain terrain)
    {
        var data = terrain.terrainData;
        var trees = data.treeInstances.Select(t => Vector3.Scale(t.position, data.size) + terrain.transform.position).ToList();
        Physics.SyncTransforms();
        for (int ring = 0; ring < 6; ring++)
        {
            for (int k = 0; k < Mathf.Max(1, ring * 6); k++)
            {
                float a = k * Mathf.PI * 2f / Mathf.Max(1, ring * 6);
                var c = new Vector3(-20f, 0f, 21f) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ring * 1.5f;
                c.y = terrain.SampleHeight(c) + terrain.transform.position.y;
                bool blocked = Physics.OverlapSphere(c + Vector3.up * 1.2f, PondRadius + 0.9f)
                    .Any(col => col is not TerrainCollider);
                if (blocked || trees.Any(t => Vector2.Distance(new Vector2(t.x, t.z), new Vector2(c.x, c.z)) < PondRadius + 1.5f)) continue;
                return c;
            }
        }
        return Vector3.negativeInfinity;
    }

    static string AddPond(Shader surface, Texture2D noise, Material foliage)
    {
        var terrain = Object.FindFirstObjectByType<Terrain>();
        var data = terrain.terrainData;
        var origin = terrain.transform.position;
        var center = pondCenter ?? FindPondSpot(terrain);
        if (float.IsNegativeInfinity(center.x)) return "pond=noSpace";

        // The terrain sits at its minimum height here, so it cannot be dug; the water lies on top and
        // paints its own depth. Clear the terrain grass under it so blades do not poke through.
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int dw = data.detailWidth, dh = data.detailHeight;
            int dx0 = Mathf.Clamp((int)((center.x - PondRadius * 1.1f - origin.x) / data.size.x * dw), 0, dw - 1);
            int dz0 = Mathf.Clamp((int)((center.z - PondRadius * 1.1f - origin.z) / data.size.z * dh), 0, dh - 1);
            int dx1 = Mathf.Clamp((int)((center.x + PondRadius * 1.1f - origin.x) / data.size.x * dw), 0, dw - 1);
            int dz1 = Mathf.Clamp((int)((center.z + PondRadius * 1.1f - origin.z) / data.size.z * dh), 0, dh - 1);
            var detail = data.GetDetailLayer(dx0, dz0, dx1 - dx0 + 1, dz1 - dz0 + 1, layer);
            for (int z = 0; z <= dz1 - dz0; z++)
                for (int x = 0; x <= dx1 - dx0; x++)
                {
                    float wx = origin.x + (dx0 + x + 0.5f) / dw * data.size.x, wz = origin.z + (dz0 + z + 0.5f) / dh * data.size.z;
                    if (new Vector2(wx - center.x, wz - center.z).magnitude < PondRadius * 1.05f) detail[z, x] = 0;
                }
            data.SetDetailLayer(dx0, dz0, layer, detail);
        }
        EditorUtility.SetDirty(data);

        var root = new GameObject("StyleTest_Pond");
        root.transform.position = center;
        float water = 0.035f;

        // Water disc.
        var v = new List<Vector3> { new Vector3(0, water, 0) };
        var n = new List<Vector3> { Vector3.up };
        var uv = new List<Vector2> { Vector2.zero };
        var t = new List<int>();
        const int seg = 40; // UV x = 0 at the centre, 1 at the shore
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2 / seg;
            float r = PondRadius * (0.97f + Mathf.Sin(a * 3f) * 0.02f);
            v.Add(new Vector3(Mathf.Cos(a) * r, water, Mathf.Sin(a) * r));
            n.Add(Vector3.up);
            uv.Add(new Vector2(1f, 0f));
            if (i > 0) t.AddRange(new[] { 0, i + 1, i });
        }
        var waterGo = new GameObject("Water");
        waterGo.transform.SetParent(root.transform, false);
        waterGo.AddComponent<MeshFilter>().sharedMesh = BuildMesh("PondWater", v, n, uv, uv.Select(_ => Vector2.zero).ToList(), t);
        var waterMat = NewMat("PondWater", Shader.Find("NailIt/WarmWater"), m => m.SetTexture("_PaintNoise", Tex("PaintNoise")));
        var waterRenderer = waterGo.AddComponent<MeshRenderer>();
        waterRenderer.sharedMaterial = waterMat;
        waterRenderer.shadowCastingMode = ShadowCastingMode.Off;

        // Keeps the player out of the water; the NavMesh was not rebaked for the test.
        var block = new GameObject("PondBlocker");
        block.transform.SetParent(root.transform, false);
        var capsuleMesh = MakeCylinder(PondRadius * 0.85f, 1.6f, 14);
        block.AddComponent<MeshCollider>().sharedMesh = capsuleMesh;
        block.GetComponent<MeshCollider>().convex = true;
        block.transform.localPosition = new Vector3(0, -0.8f, 0);
        // Carve the baked NavMesh too, so the dog, mailman and other agents path around the water.
        var carve = block.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        carve.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
        carve.radius = PondRadius * 0.9f;
        carve.height = 1.6f;
        carve.center = new Vector3(0, 0.8f, 0);
        carve.carving = true;

        // Shore stones.
        var stoneMat = NewMat("Stone", surface, m =>
        {
            m.SetColor("_BaseColor", new Color(0.56f, 0.51f, 0.46f));
            SetStyle(m, Tex("PaintNoise"), 0.5f);
        });
        var stones = Enumerable.Range(0, 3).Select(i => MakeStone(31 + i)).ToArray();
        var rng = new System.Random(5);
        float R() => (float)rng.NextDouble();
        for (int i = 0; i < 17; i++)
        {
            float a = i * Mathf.PI * 2 / 17 + R() * 0.2f;
            if (a > 4.3f && a < 5.1f) continue; // leave a gap facing the house
            float r = PondRadius * (0.98f + R() * 0.14f);
            var p = center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            p.y = terrain.SampleHeight(p) + origin.y - 0.05f;
            var s = new GameObject("Stone");
            s.transform.SetParent(root.transform, true);
            s.transform.SetPositionAndRotation(p, Quaternion.Euler(0, R() * 360, 0));
            float size = 0.22f + R() * 0.25f;
            s.transform.localScale = new Vector3(size * 1.3f, size * 0.7f, size);
            s.AddComponent<MeshFilter>().sharedMesh = stones[i % 3];
            s.AddComponent<MeshRenderer>().sharedMaterial = stoneMat;
        }

        // Reeds on three sides and lily pads on the water.
        foreach (float a in new[] { 0.6f, 2.4f, 3.7f })
        {
            var p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * PondRadius * 0.92f;
            p.y = terrain.SampleHeight(p) + origin.y - 0.05f;
            var reeds = new GameObject("Reeds");
            reeds.transform.SetParent(root.transform, true);
            reeds.transform.position = p;
            reeds.AddComponent<MeshFilter>().sharedMesh = MakeReeds(rng);
            reeds.AddComponent<MeshRenderer>().sharedMaterial = foliage;
        }
        var pads = new GameObject("LilyPads");
        pads.transform.SetParent(root.transform, false);
        pads.transform.localPosition = new Vector3(0, water + 0.012f, 0);
        pads.AddComponent<MeshFilter>().sharedMesh = MakeLilyPads(rng);
        var padRenderer = pads.AddComponent<MeshRenderer>();
        padRenderer.sharedMaterial = foliage;
        padRenderer.shadowCastingMode = ShadowCastingMode.Off;

        Physics.SyncTransforms();
        return $"pond={center}";
    }

    static Mesh MakeCylinder(float radius, float height, int sides)
    {
        var v = new List<Vector3>();
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2 / sides;
            v.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            v.Add(new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius));
        }
        var t = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            int a = i * 2, b = (i + 1) % sides * 2;
            t.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
        }
        var mesh = new Mesh { name = "PondBlocker" };
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        AddMesh(mesh);
        return mesh;
    }

    static Mesh MakeStone(int seed)
    {
        var (iv, it) = Ico();
        var v = iv.Select(p => p * (0.85f + Hash((int)(p.x * 97), (int)(p.z * 89), seed) * 0.3f)).ToList();
        var mesh = new Mesh { name = "Stone_" + seed };
        mesh.SetVertices(v);
        mesh.SetTriangles(it, 0);
        mesh.SetUVs(0, v.Select(_ => Vector2.zero).ToList());
        mesh.SetUVs(1, v.Select(_ => Vector2.zero).ToList());
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AddMesh(mesh);
        return mesh;
    }

    static Mesh MakeReeds(System.Random rng)
    {
        float R() => (float)rng.NextDouble();
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var w = new List<Vector2>();
        var t = new List<int>();
        for (int b = 0; b < 10; b++)
        {
            var root = new Vector3((R() - 0.5f) * 0.6f, 0, (R() - 0.5f) * 0.6f);
            float h = 0.7f + R() * 0.7f;
            var lean = new Vector3((R() - 0.5f) * 0.3f, 0, (R() - 0.5f) * 0.3f);
            float hue = 0.15f + R() * 0.25f;
            for (int side = 0; side < 2; side++)
            {
                var across = side == 0 ? Vector3.right : Vector3.forward;
                int s = v.Count;
                v.AddRange(new[] { root - across * 0.035f, root + across * 0.035f, root + lean + Vector3.up * h });
                var normal = Vector3.Cross(across, Vector3.up).normalized;
                n.AddRange(new[] { normal, normal, normal });
                uv.AddRange(new[] { new Vector2(hue, 0.25f), new Vector2(hue, 0.25f), new Vector2(hue, 0.9f) });
                w.AddRange(new[] { Vector2.zero, Vector2.zero, new Vector2(1, 0) });
                t.AddRange(new[] { s, s + 2, s + 1, s, s + 1, s + 2 }); // both sides
            }
            if (b % 3 == 0)
            {
                // Cattail head: a small brown diamond near the top.
                var c = root + lean * 0.85f + Vector3.up * h * 0.85f;
                int s = v.Count;
                Vector3[] d = { Vector3.up * 0.09f, Vector3.down * 0.09f, Vector3.left * 0.03f, Vector3.right * 0.03f, Vector3.forward * 0.03f, Vector3.back * 0.03f };
                foreach (var p in d)
                {
                    v.Add(c + p);
                    n.Add(p.normalized);
                    uv.Add(new Vector2(UTrunk, 0.25f));
                    w.Add(new Vector2(0.85f, 0));
                }
                t.AddRange(new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 }.Select(i => s + i));
            }
        }
        return BuildMesh("Reeds", v, n, uv, w, t);
    }

    static Mesh MakeLilyPads(System.Random rng)
    {
        float R() => (float)rng.NextDouble();
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        for (int p = 0; p < 7; p++)
        {
            float a0 = R() * Mathf.PI * 2, d = PondRadius * (0.25f + R() * 0.55f);
            var c = new Vector3(Mathf.Cos(a0) * d, 0, Mathf.Sin(a0) * d);
            float r = 0.18f + R() * 0.16f, notch = R() * Mathf.PI * 2;
            int s = v.Count;
            v.Add(c);
            n.Add(Vector3.up);
            uv.Add(new Vector2(0.32f, 0.75f));
            const int seg = 12;
            for (int i = 0; i <= seg; i++)
            {
                float a = notch + 0.45f + i * (Mathf.PI * 2 - 0.9f) / seg;
                v.Add(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r);
                n.Add(Vector3.up);
                uv.Add(new Vector2(0.32f, 0.55f));
                if (i > 0) t.AddRange(new[] { s, s + i + 1, s + i });
            }
            if (p % 3 == 0)
            {
                // Small pink-white flower on some pads.
                int f = v.Count;
                Vector3[] petals = { Vector3.up * 0.06f, Vector3.left * 0.06f, Vector3.right * 0.06f, Vector3.forward * 0.06f, Vector3.back * 0.06f, Vector3.zero };
                foreach (var q in petals)
                {
                    v.Add(c + q + Vector3.up * 0.02f);
                    n.Add((q + Vector3.up * 0.05f).normalized);
                    uv.Add(new Vector2(UFlower, 0.5f));
                }
                t.AddRange(new[] { 0, 3, 2, 0, 2, 4, 0, 4, 1, 0, 1, 3, 5, 2, 3, 5, 4, 2, 5, 1, 4, 5, 3, 1 }.Select(i => f + i));
            }
        }
        return BuildMesh("LilyPads", v, n, uv, uv.Select(_ => Vector2.zero).ToList(), t);
    }

    // ---------- Ambience ----------

    static Mesh MakeWingQuad()
    {
        var v = new List<Vector3> { new(-0.5f, 0, -0.5f), new(0, 0, -0.5f), new(0.5f, 0, -0.5f), new(-0.5f, 0, 0.5f), new(0, 0, 0.5f), new(0.5f, 0, 0.5f) };
        var uv = v.Select(p => new Vector2(p.x + 0.5f, p.z + 0.5f)).ToList();
        var t = new List<int> { 0, 3, 4, 0, 4, 1, 1, 4, 5, 1, 5, 2 };
        return BuildMesh("Wings", v, v.Select(_ => Vector3.up).ToList(), uv, uv.Select(_ => Vector2.zero).ToList(), t);
    }

    static void AddAmbience(Light sun)
    {
        var flap = Shader.Find("NailIt/FlapWings");
        var wings = MakeWingQuad();
        var butterflyMats = new[]
        {
            (new Color(1f, 0.84f, 0.25f), new Color(0.38f, 0.22f, 0.08f)),
            (new Color(0.98f, 0.96f, 0.9f), new Color(0.45f, 0.42f, 0.4f)),
            (new Color(1f, 0.55f, 0.16f), new Color(0.13f, 0.08f, 0.06f)),
        }.Select((c, i) =>
        {
            return NewMat("Butterfly_" + i, flap, m =>
            {
                m.SetColor("_Color", c.Item1);
                m.SetColor("_EdgeColor", c.Item2);
            });
        }).ToArray();
        var birdMat = NewMat("Bird", flap, m =>
        {
            m.SetColor("_Color", new Color(0.33f, 0.25f, 0.21f));
            m.SetColor("_EdgeColor", new Color(0.18f, 0.13f, 0.11f));
            m.SetFloat("_Shape", 1f);
            m.SetFloat("_FlapSpeed", 7f);
            m.SetFloat("_FlapAngle", 0.55f);
        });

        // Spots butterflies visit: planters, the pond and the yard.
        var spots = new List<Vector3>();
        var dressing = GameObject.Find("StyleTest_Dressing");
        if (dressing) foreach (Transform c in dressing.transform) if (c.name == "Planter") spots.Add(c.position + Vector3.up * 0.5f);
        var pond = GameObject.Find("StyleTest_Pond");
        if (pond) spots.Add(pond.transform.position + Vector3.up * 0.4f);
        spots.Add(HutCenter + new Vector3(-6f, 0.5f, -3.5f));
        spots.Add(HutCenter + new Vector3(6f, 0.5f, -3.5f));

        var root = new GameObject("StyleTest_Ambience");
        var butterflies = new GameObject("Butterflies").transform;
        butterflies.SetParent(root.transform);
        var rng = new System.Random(11);
        float R() => (float)rng.NextDouble();
        for (int i = 0; i < 12; i++)
        {
            var b = new GameObject("Butterfly");
            b.transform.SetParent(butterflies);
            var spot = spots[i % spots.Count];
            b.transform.SetPositionAndRotation(spot + new Vector3((R() - 0.5f) * 2f, R() * 0.8f, (R() - 0.5f) * 2f), Quaternion.Euler(0, R() * 360, 0));
            b.transform.localScale = Vector3.one * (0.14f + R() * 0.06f);
            b.AddComponent<MeshFilter>().sharedMesh = wings;
            var r = b.AddComponent<MeshRenderer>();
            r.sharedMaterial = butterflyMats[i % 3];
        }

        var birds = new GameObject("Birds").transform;
        birds.SetParent(root.transform);
        for (int i = 0; i < 4; i++)
        {
            var b = new GameObject("Bird");
            b.transform.SetParent(birds);
            b.transform.position = HutCenter + new Vector3(i * 3f - 4f, 7.5f, 6f);
            b.transform.localScale = Vector3.one * 0.55f;
            b.AddComponent<MeshFilter>().sharedMesh = wings;
            b.AddComponent<MeshRenderer>().sharedMaterial = birdMat;
        }

        // Birds sing from the nearest trees.
        var foliage = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Foliage.mat");
        var trees = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.sharedMaterial == foliage && r.bounds.size.y > 2.2f)
            .Select(r => r.bounds.center + Vector3.up * r.bounds.extents.y * 0.5f)
            .ToList();
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain)
        {
            var td = terrain.terrainData;
            trees.AddRange(td.treeInstances.Select(ti => Vector3.Scale(ti.position, td.size) + terrain.transform.position + Vector3.up * 3f));
        }
        trees = trees.OrderBy(p => (p - HutCenter).sqrMagnitude).Take(8).ToList();
        if (trees.Count == 0) trees.Add(HutCenter + Vector3.up * 4f);

        var ambience = root.AddComponent<StyleTestAmbience>();
        ambience.SetData(butterflies, birds, spots, trees, HutCenter, sun);
        ambience.SetChirps(AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToList());
        EditorUtility.SetDirty(ambience);
    }

    // Soft pollen drifting in the sunlight around the player.
    static void AddMotes()
    {
        var cm = Object.FindFirstObjectByType<CinemachineCamera>();
        var follow = cm != null && cm.Follow != null ? cm.Follow : null;
        var go = new GameObject("StyleTest_Pollen");
        if (follow) go.transform.SetParent(follow, false);
        else go.transform.position = HutCenter;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = 0.05f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
        main.maxParticles = 150;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 0.93f, 0.72f, 0.75f);
        main.gravityModifier = -0.002f;
        var emission = ps.emission;
        emission.rateOverTime = 16f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 3f, 16f);
        shape.position = new Vector3(0f, 1.6f, 2f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = NewMat("Pollen", Shader.Find("NailIt/Mote"));
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
    }

    // Slow cloud shadows drifting over everything, using the sun's light cookie.
    static void BuildCloudTexture()
    {
        const int size = 256;
        var px = new Color32[size * size];
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                float n = Fbm(x / (float)size, y / (float)size, 3, 4, 123);
                byte v = (byte)(Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.6f, n))) * 255);
                px[y * size + x] = new Color32(v, v, v, 255);
            }
        });
        SaveTexture("CloudShadows", px, size, size, FilterMode.Bilinear, false);
    }

    static void ApplyCloudCookie(Light sun)
    {
        sun.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/CloudShadows.png");
        if (sun.TryGetComponent<UniversalAdditionalLightData>(out var data))
        {
            data.lightCookieSize = new Vector2(55f, 55f);
            EditorUtility.SetDirty(data);
        }
        EditorUtility.SetDirty(sun);
    }
}
