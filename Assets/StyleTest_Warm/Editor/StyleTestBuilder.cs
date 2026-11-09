using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds the warm style test. Everything it creates stays inside Assets/StyleTest_Warm.
public static partial class StyleTestBuilder
{
    const string Root = "Assets/StyleTest_Warm";
    const string ScenePath = Root + "/Level_01_WarmStyle.unity";
    const string SourceScene = "Assets/Scenes/Level_01.unity";
    const string Gen = Root + "/Generated";
    const string TexDir = Gen + "/Textures";
    const string MatDir = Gen + "/Materials";

    static readonly Color ShadowFill = new Color(0.27f, 0.19f, 0.36f);
    static readonly Color RimColor = new Color(0.20f, 0.11f, 0.05f);

    public static string BuildAll()
    {
        EnsureFolder(Root, "Generated");
        EnsureFolder(Gen, "Textures");
        EnsureFolder(Gen, "Materials");

        if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(SourceScene, ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var noise = BuildNoise();
        var surface = Shader.Find("NailIt/WarmStylized");
        var dither = Shader.Find("Hidden/OcclusionDither/WarmStylized");
        if (surface == null || dither == null) return "shaders not found";

        var map = BuildMaterialMap(surface, noise);
        int swapped = ApplyMaterials(map);
        int foliage = ApplyFoliage(surface, noise);
        var foliageMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Foliage.mat"); var terrainLog = BuildTerrain(noise, foliageMat); ApplyLampGlow(surface, noise); AddDressing(foliageMat); string pondLog = AddPond(surface, noise, foliageMat); AddFlowerScatter(foliageMat); BuildCloudTexture(); AddDog(map);
        ApplyLighting();
        ApplyVolumeAndCamera();
        var sunLight = GameObject.Find("Directional Light_Real").GetComponent<Light>();
        AddAmbience(sunLight);
        AddMotes();

        var host = GameObject.Find("StyleTest_Runtime") ?? new GameObject("StyleTest_Runtime");
        if (!host.TryGetComponent<StyleTestRuntime>(out var runtime)) runtime = host.AddComponent<StyleTestRuntime>();
        int missing = RelinkTextures(map); ReapplyMaterialSetup(); ApplyCloudCookie(sunLight); runtime.SetData(map.Keys.ToList(), map.Values.ToList(), surface, dither);
        EditorUtility.SetDirty(runtime);

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"missingTextures={missing} materials={map.Count} slotsSwapped={swapped} foliage={foliage} {terrainLog} {pondLog}";
    }

    // ---------- Textures ----------

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    // Imports can reload material assets from disk, so write pending material edits first.
    static void FlushMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null) EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    static Texture2D SaveTexture(string name, Color32[] px, int w, int h, FilterMode filter, bool mips = true, bool alpha = false)
    {
        FlushMaterials();
        string path = TexDir + "/" + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.maxTextureSize = 2048;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.mipmapEnabled = mips;
        imp.filterMode = filter;
        imp.alphaIsTransparency = alpha;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    // Seamless fractal value noise.
    static float Fbm(float u, float v, int period, int octaves, int seed)
    {
        float sum = 0, amp = 0.5f, total = 0;
        for (int o = 0; o < octaves; o++)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int M(int a) => ((a % period) + period) % period;
            float a00 = Hash(M(x0), M(y0), seed + o), a10 = Hash(M(x0 + 1), M(y0), seed + o);
            float a01 = Hash(M(x0), M(y0 + 1), seed + o), a11 = Hash(M(x0 + 1), M(y0 + 1), seed + o);
            sum += amp * Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
            total += amp;
            amp *= 0.5f;
            period *= 2;
        }
        return sum / total;
    }

    static Texture2D BuildNoise()
    {
        const int size = 256;
        var px = new Color32[size * size];
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float a = Mathf.Clamp01((Fbm(u, v, 6, 4, 11) - 0.5f) * 2.4f + 0.5f);
                float b = Mathf.Clamp01((Fbm(u, v, 14, 3, 37) - 0.5f) * 2.4f + 0.5f);
                px[y * size + x] = new Color32((byte)(a * 255), (byte)(b * 255), 128, 255);
            }
        });
        return SaveTexture("PaintNoise", px, size, size, FilterMode.Bilinear);
    }

    static Color32 Grade(Color32 c)
    {
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
        float lum = r * 0.3f + g * 0.59f + b * 0.11f;
        r = lum + (r - lum) * 1.2f;
        g = lum + (g - lum) * 1.2f;
        b = lum + (b - lum) * 1.2f;
        r = r * 1.03f * 0.93f + 0.06f;
        g = g * 0.98f * 0.93f + 0.045f;
        b = b * 0.95f * 0.93f + 0.05f;
        return new Color32((byte)(Mathf.Clamp01(r) * 255), (byte)(Mathf.Clamp01(g) * 255), (byte)(Mathf.Clamp01(b) * 255), c.a);
    }

    public static Color GradeColor(Color c)
    {
        var g = Grade(new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), 255));
        return new Color(g.r / 255f, g.g / 255f, g.b / 255f, c.a);
    }

    // Kuwahara filter via summed-area tables: turns photo detail into flat brush-like patches.
    static Color32[] Kuwahara(Color32[] src, int w, int h, int radius)
    {
        int W = w + 1;
        var sr = new double[W * (h + 1)];
        var sg = new double[W * (h + 1)];
        var sb = new double[W * (h + 1)];
        var sl = new double[W * (h + 1)];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = src[y * w + x];
                double l = c.r * 0.3 + c.g * 0.59 + c.b * 0.11;
                int i = (y + 1) * W + x + 1, up = y * W + x + 1, left = (y + 1) * W + x, diag = y * W + x;
                sr[i] = c.r + sr[up] + sr[left] - sr[diag];
                sg[i] = c.g + sg[up] + sg[left] - sg[diag];
                sb[i] = c.b + sb[up] + sb[left] - sb[diag];
                sl[i] = l * l + sl[up] + sl[left] - sl[diag];
            }
        }
        var dst = new Color32[src.Length];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double best = double.MaxValue;
                byte br = 0, bg = 0, bb = 0;
                for (int q = 0; q < 4; q++)
                {
                    int x0 = Mathf.Max((q & 1) == 0 ? x - radius : x, 0), x1 = Mathf.Min((q & 1) == 0 ? x : x + radius, w - 1) + 1;
                    int y0 = Mathf.Max((q & 2) == 0 ? y - radius : y, 0), y1 = Mathf.Min((q & 2) == 0 ? y : y + radius, h - 1) + 1;
                    double n = (x1 - x0) * (y1 - y0);
                    double Sum(double[] s) => s[y1 * W + x1] - s[y0 * W + x1] - s[y1 * W + x0] + s[y0 * W + x0];
                    double mr = Sum(sr) / n, mg = Sum(sg) / n, mb = Sum(sb) / n;
                    double ml = mr * 0.3 + mg * 0.59 + mb * 0.11;
                    double variance = Sum(sl) / n - ml * ml;
                    if (variance < best)
                    {
                        best = variance;
                        br = (byte)mr;
                        bg = (byte)mg;
                        bb = (byte)mb;
                    }
                }
                dst[y * w + x] = new Color32(br, bg, bb, src[y * w + x].a);
            }
        });
        return dst;
    }

    static readonly Dictionary<string, Texture2D> paintedByHash = new();

    static Texture2D PaintedCopy(Texture src)
    {
        if (!(src is Texture2D)) return src as Texture2D;
        string path = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) return src as Texture2D;

        byte[] bytes = File.ReadAllBytes(path);
        string hash;
        using (var md5 = MD5.Create()) hash = System.BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").Substring(0, 12);
        if (paintedByHash.TryGetValue(hash, out var cached)) return cached;

        string outName = "Painted_" + hash;
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + outName + ".png");
        if (existing != null)
        {
            paintedByHash[hash] = existing;
            return existing;
        }

        Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (!(ext is ".png" or ".jpg" or ".jpeg") || !readable.LoadImage(bytes))
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            readable = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }

        // Flat palettes only need grading: filtering would smear their swatches.
        bool palette = bytes.Length < 300 * 1024 || src.name.StartsWith("Atlas (1)") || src.name.Contains("Vehicle");
        var px = readable.GetPixels32();
        int w = readable.width, h = readable.height;
        Object.DestroyImmediate(readable);
        if (!palette) px = Kuwahara(px, w, h, w >= 2000 ? 3 : 2);
        Parallel.For(0, px.Length, i => px[i] = Grade(px[i]));

        var srcImp = AssetImporter.GetAtPath(path) as TextureImporter;
        var painted = SaveTexture(outName, px, w, h, src.filterMode, true, srcImp != null && srcImp.alphaIsTransparency);
        paintedByHash[hash] = painted;
        return painted;
    }

    // ---------- Materials ----------

    static bool Convertible(Material m)
    {
        if (m == null) return false;
        string s = m.shader.name;
        if (s != "Universal Render Pipeline/Lit" && s != "Universal Render Pipeline/Simple Lit" && s != "Toon/Toon") return false;
        if (m.renderQueue >= 2900) return false;
        return !(m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f);
    }

    static void SetStyle(Material t, Texture2D noise, float paint = 0.42f)
    {
        t.SetTexture("_PaintNoise", noise);
        t.SetFloat("_PaintStrength", paint); t.SetFloat("_PaintScale", 0.55f);
        t.SetColor("_ShadowTint", ShadowFill);
        t.SetColor("_RimColor", RimColor);
    }

    static Dictionary<Material, Material> BuildMaterialMap(Shader surface, Texture2D noise)
    {
        var sources = new HashSet<Material>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
                if (Convertible(m)) sources.Add(m);

        // Spawned tiles, cars and carriables use these.
        // Prefabs_MP too: the levels are shared with multiplayer, which spawns its own copies.
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Prefabs_MP" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.sharedMaterials)
                    if (Convertible(m)) sources.Add(m);
            }
        }

        var map = new Dictionary<Material, Material>();
        foreach (var src in sources)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
            if (string.IsNullOrEmpty(guid)) continue;
            string path = MatDir + "/" + Sanitize(src.name) + "_" + guid.Substring(0, 6) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(surface);
                AssetDatabase.CreateAsset(mat, path);
            }

            Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
            if (tex == null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            Color col = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
            mat.SetTexture("_BaseMap", tex != null ? PaintedCopy(tex) : null);
            mat.SetColor("_BaseColor", tex != null ? col : GradeColor(col));
            if (src.HasProperty("_BaseMap"))
            {
                mat.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                mat.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
            }
            SetStyle(mat, noise);

            bool clip = src.IsKeywordEnabled("_ALPHATEST_ON") || (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f);
            SetKeyword(mat, "_ALPHATEST_ON", clip);
            if (clip && src.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", src.GetFloat("_Cutoff"));

            bool emissive = src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor");
            SetKeyword(mat, "_EMISSION", emissive);
            if (emissive)
            {
                mat.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
                var em = src.HasProperty("_EmissionMap") ? src.GetTexture("_EmissionMap") : null;
                mat.SetTexture("_EmissionMap", em != null ? em : Texture2D.whiteTexture);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            map[src] = mat;
        }
        return map;
    }

    static void SetKeyword(Material m, string k, bool on)
    {
        if (on) m.EnableKeyword(k);
        else m.DisableKeyword(k);
    }

    static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    static int ApplyMaterials(Dictionary<Material, Material> map)
    {
        int count = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer) continue;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && map.TryGetValue(mats[i], out var t))
                {
                    mats[i] = t;
                    changed = true;
                    count++;
                }
            }
            if (!changed) continue;
            r.sharedMaterials = mats;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            EditorUtility.SetDirty(r);
        }
        return count;
    }

    // ---------- Foliage ----------

    static readonly string[] FoliageNames = { "papatya çalısı", "elma çalısı", "normal ağaç" };
    const float UFlower = 0.75f, UYellow = 0.865f, UApple = 0.95f, UTrunk = 0.6f;

    static int ApplyFoliage(Shader surface, Texture2D noise)
    {
        var ramp = BuildFoliageRamp();
        var mat = NewMat("Foliage", surface, m =>
        {
            m.SetTexture("_BaseMap", Tex("FoliageRamp"));
            m.SetColor("_BaseColor", Color.white);
            SetStyle(m, Tex("PaintNoise"), 0.45f);
            m.SetFloat("_RampSoft", 0.35f);
            m.SetFloat("_RampOffset", -0.05f);
            m.SetFloat("_Wind", 1f);
            m.EnableKeyword("_WIND");
            m.SetFloat("_WindAmount", 0.07f);
        });

        string meshPath = Gen + "/FoliageMeshes.asset";
        var holder = AssetDatabase.LoadAssetAtPath<FoliageMeshHolder>(meshPath);
        if (holder == null)
        {
            holder = ScriptableObject.CreateInstance<FoliageMeshHolder>();
            AssetDatabase.CreateAsset(holder, meshPath);
        }
        var cache = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First()); // earlier test builds left duplicate names

        int count = 0;
        foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var old = f.sharedMesh;
            if (old == null) continue;
            string src = AssetDatabase.GetAssetPath(old);
            if (string.IsNullOrEmpty(src) || src == meshPath) continue;
            string file = Path.GetFileNameWithoutExtension(src);
            if (!FoliageNames.Any(n => file.Contains(n))) continue;

            string key = Sanitize(file) + "_" + old.name;
            if (!cache.TryGetValue(key, out var mesh))
            {
                mesh = MakeFoliage(old.bounds, file.Contains("ağaç"), file.Contains("papatya") ? UFlower : file.Contains("elma") ? UApple : -1f, key.GetHashCode());
                mesh.name = key;
                AssetDatabase.AddObjectToAsset(mesh, holder);
                cache[key] = mesh;
            }
            f.sharedMesh = mesh;
            var r = f.GetComponent<MeshRenderer>();
            r.sharedMaterials = new[] { mat };
            PrefabUtility.RecordPrefabInstancePropertyModifications(f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            count++;
        }
        return count;
    }

    static Texture2D BuildFoliageRamp()
    {
        const int size = 64;
        var px = new Color32[size * size];
        Color dark = new Color(0.13f, 0.30f, 0.11f), mid = new Color(0.33f, 0.52f, 0.13f), light = new Color(0.68f, 0.76f, 0.24f);
        Color darkB = new Color(0.11f, 0.27f, 0.14f), midB = new Color(0.25f, 0.47f, 0.17f), lightB = new Color(0.52f, 0.70f, 0.24f);
        for (int y = 0; y < size; y++)
        {
            float v = y / (size - 1f);
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f);
                Color c;
                if (u < 0.5f)
                {
                    float hue = u / 0.5f;
                    Color a = Color.Lerp(dark, darkB, hue), b = Color.Lerp(mid, midB, hue), d = Color.Lerp(light, lightB, hue);
                    c = v < 0.55f ? Color.Lerp(a, b, v / 0.55f) : Color.Lerp(b, d, (v - 0.55f) / 0.45f);
                }
                else if (u < 0.68f) c = Color.Lerp(new Color(0.24f, 0.14f, 0.10f), new Color(0.48f, 0.31f, 0.18f), v);
                else if (u < 0.83f) c = new Color(1f, 0.97f, 0.85f);
                else if (u < 0.9f) c = new Color(1f, 0.8f, 0.25f);
                else c = new Color(0.86f, 0.2f, 0.14f);
                px[y * size + x] = c;
            }
        }
        return SaveTexture("FoliageRamp", px, size, size, FilterMode.Bilinear, false);
    }

    static (Vector3[] verts, int[] tris) Ico()
    {
        float t = (1 + Mathf.Sqrt(5)) / 2;
        var v = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
            new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
            new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
        var mid = new Dictionary<(int, int), int>();
        int Mid(int a, int b)
        {
            var k = a < b ? (a, b) : (b, a);
            if (mid.TryGetValue(k, out int i)) return i;
            v.Add(((v[a] + v[b]) * 0.5f).normalized * v[a].magnitude);
            mid[k] = v.Count - 1;
            return v.Count - 1;
        }
        var tris = new List<int>();
        for (int i = 0; i < f.Length; i += 3)
        {
            int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        return (v.Select(p => p.normalized).ToArray(), tris.ToArray());
    }

    // Puffy clumps fitted to the old mesh's bounds, so transforms and colliders stay valid.
    static Mesh MakeFoliage(Bounds b, bool tree, float accentU, int seed)
    {
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        var (iv, it) = Ico();
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float bottom = tree ? b.min.y + b.size.y * 0.42f : b.min.y;
        var canopyC = new Vector3(b.center.x, (bottom + b.max.y) * 0.5f, b.center.z);
        var canopyE = new Vector3(b.extents.x, (b.max.y - bottom) * 0.5f, b.extents.z);
        int clumps = tree ? 13 : 9;
        float clumpR = Mathf.Min(canopyE.x, canopyE.y, canopyE.z) * (tree ? 0.62f : 0.72f);

        void AddClump(Vector3 c, float rad, float u, bool shadeHeight)
        {
            int start = verts.Count;
            foreach (var n in iv)
            {
                float wobble = 1 + (R() - 0.5f) * 0.16f;
                var p = c + n * rad * wobble;
                verts.Add(p);
                if (shadeHeight)
                {
                    var outward = Vector3.Scale(p - canopyC, new Vector3(1 / canopyE.x, 1 / canopyE.y, 1 / canopyE.z)).normalized;
                    norms.Add(Vector3.Lerp(n, outward, 0.55f).normalized);
                    float h = Mathf.InverseLerp(canopyC.y - canopyE.y, canopyC.y + canopyE.y, p.y);
                    uvs.Add(new Vector2(u, Mathf.Clamp01(0.08f + h * 0.6f + n.y * 0.3f)));
                }
                else
                {
                    norms.Add(n);
                    uvs.Add(new Vector2(u, 0.5f));
                }
            }
            foreach (int i in it) tris.Add(start + i);
        }

        var surfacePoints = new List<Vector3>();
        for (int i = 0; i < clumps; i++)
        {
            // Golden-angle spiral on the ellipsoid gives even coverage with few clumps.
            float k = (i + 0.5f) / clumps;
            float y = 1 - 1.5f * k;
            float rad = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
            float a = i * 2.39996f + R() * 0.5f;
            var dir = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
            var c = canopyC + Vector3.Scale(dir, canopyE) * 0.58f;
            float size = clumpR * (0.85f + R() * 0.35f);
            AddClump(c, size, R() * 0.45f, true);
            surfacePoints.Add(c + dir.normalized * size * 0.92f);
        }
        AddClump(canopyC, Mathf.Min(canopyE.x, canopyE.y, canopyE.z) * 0.8f, 0.2f, true);

        if (tree)
        {
            int start = verts.Count;
            const int sides = 7;
            float rTop = b.extents.x * 0.09f, rBottom = b.extents.x * 0.15f;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2;
                var n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts.Add(new Vector3(b.center.x, b.min.y, b.center.z) + n * rBottom);
                verts.Add(new Vector3(b.center.x, canopyC.y, b.center.z) + n * rTop);
                norms.Add(n);
                norms.Add(n);
                uvs.Add(new Vector2(UTrunk, 0.2f));
                uvs.Add(new Vector2(UTrunk, 0.9f));
            }
            for (int i = 0; i < sides; i++)
            {
                int s = start + i * 2;
                tris.AddRange(new[] { s, s + 1, s + 3, s, s + 3, s + 2 });
            }
        }

        if (accentU > 0)
        {
            foreach (var p in surfacePoints)
                { AddClump(p, clumpR * 0.2f, accentU, false); AddClump(p + new Vector3(clumpR * 0.45f, -clumpR * 0.25f, clumpR * 0.2f), clumpR * 0.16f, accentU, false); }
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        // Wind weight in UV1: 0 at the base, 1 at the top.
        mesh.SetUVs(1, verts.Select(v => new Vector2(Mathf.Pow(Mathf.InverseLerp(b.min.y, b.max.y, v.y), 1.5f), 0)).ToList());
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- Terrain ----------

    static string BuildTerrain(Texture2D noise, Material foliage)
    {
        var terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) return "noTerrain";

        string path = Gen + "/Terrain_Warm.asset";
        if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain.terrainData)) != AssetDatabase.AssetPathToGUID(path))
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(terrain.terrainData), path);
            var copy = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            terrain.terrainData = copy;
            var col = terrain.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = copy;
            EditorUtility.SetDirty(terrain);
            ReshapeSplat(copy);
        }

        var data = terrain.terrainData;
        var grass = MakeGround("Ground_Grass", new Color(0.31f, 0.42f, 0.16f), new Color(0.44f, 0.53f, 0.21f), new Color(0.57f, 0.60f, 0.27f), 21);
        var dirt = MakeGround("Ground_Dirt", new Color(0.56f, 0.38f, 0.21f), new Color(0.72f, 0.53f, 0.31f), new Color(0.83f, 0.65f, 0.41f), 58);
        var layers = data.terrainLayers.ToArray();
        layers[0] = MakeLayer("Layer_Dirt", dirt, layers[0].tileSize);
        layers[1] = MakeLayer("Layer_Grass", grass, new Vector2(12, 12));
        data.terrainLayers = layers;

        // Warm copies of the wind grass, used only by this terrain copy.
        var protos = data.detailPrototypes;
        for (int i = 0; i < protos.Length; i++)
        {
            if (protos[i].prototype == null) continue;
            protos[i].prototype = WarmDetailPrefab(protos[i].prototype, i);
            protos[i].healthyColor = Color.white;
            protos[i].dryColor = new Color(0.92f, 0.9f, 0.7f);
        }
        data.detailPrototypes = protos; ReplaceTerrainTrees(data, foliage);
        EditorUtility.SetDirty(data);
        return $"terrain={data.alphamapWidth}px";
    }

    // Most of the old ground is bare dirt; the reference is mostly grass with worn patches.
    static void ReshapeSplat(TerrainData data)
    {
        int w = data.alphamapWidth, h = data.alphamapHeight;
        var a = data.GetAlphamaps(0, 0, w, h);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n = Fbm(x / (float)w, y / (float)h, 24, 3, 77);
                float g = Mathf.Max(a[y, x, 1], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.38f, 0.5f, n)));
                a[y, x, 1] = g;
                a[y, x, 0] = 1 - g;
            }
        }
        data.SetAlphamaps(0, 0, a);
    }

    static Texture2D MakeGround(string name, Color low, Color mid, Color high, int seed)
    {
        const int size = 512;
        var px = new Color32[size * size];
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Mathf.Clamp01((Fbm(u, v, 4, 5, seed) - 0.5f) * 1.7f + 0.5f);
                float speck = Mathf.Clamp01((Fbm(u, v, 48, 2, seed + 9) - 0.5f) * 3f + 0.5f);
                Color c = n < 0.5f ? Color.Lerp(low, mid, n * 2) : Color.Lerp(mid, high, (n - 0.5f) * 2);
                c *= 0.92f + speck * 0.16f;
                c.a = 1;
                px[y * size + x] = c;
            }
        });
        return SaveTexture(name, px, size, size, FilterMode.Bilinear);
    }

    static TerrainLayer MakeLayer(string name, Texture2D tex, Vector2 tile)
    {
        string path = Gen + "/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }
        layer.diffuseTexture = tex;
        layer.tileSize = tile;
        layer.metallic = 0;
        layer.smoothness = 0;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static GameObject WarmDetailPrefab(GameObject src, int index)
    {
        string path = Gen + "/Detail_" + index + ".prefab";
        if (AssetDatabase.GetAssetPath(src) == path) return src;

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string matPath = MatDir + "/Detail_" + index + "_" + i + ".mat";
                var copy = new Material(mats[i]);
                Color warm = index == 2 ? new Color(0.72f, 0.70f, 0.22f) : new Color(0.46f, 0.62f, 0.18f);
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", warm);
                if (copy.HasProperty("_Color")) copy.SetColor("_Color", warm);
                AssetDatabase.CreateAsset(copy, matPath);
                mats[i] = copy;
            }
            r.sharedMaterials = mats;
        }
        var saved = PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
        return saved;
    }

    // ---------- Lighting, post, camera ----------

    static void ApplyLighting()
    {
        var sun = GameObject.Find("Directional Light_Real").GetComponent<Light>();
        sun.color = new Color(1f, 0.87f, 0.69f);
        sun.intensity = 1.6f; sun.cullingMask = ~(1 << 5); sun.renderingLayerMask = -1; if (sun.TryGetComponent<UniversalAdditionalLightData>(out var sunData)) { sunData.customShadowLayers = false; sunData.renderingLayers = RenderingLayerMask.defaultRenderingLayerMask | (RenderingLayerMask)uint.MaxValue; EditorUtility.SetDirty(sunData); } // The original sun only covered rendering layer 2 and culling layer 7, so almost nothing cast shadows.
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.82f;
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.75f, -0.6f, 0.28f));
        EditorUtility.SetDirty(sun);
        EditorUtility.SetDirty(sun.transform);

        string skyPath = MatDir + "/WarmSky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("NailIt/WarmSky"));
            AssetDatabase.CreateAsset(sky, skyPath);
        }
        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.63f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.63f, 0.56f, 0.56f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.32f, 0.27f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.98f, 0.76f, 0.55f);
        RenderSettings.fogDensity = 0.004f;
        DynamicGI.UpdateEnvironment();
    }

    static T Comp<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet<T>(out var c))
        {
            c = p.Add<T>(true);
            AssetDatabase.AddObjectToAsset(c, p);
        }
        return c;
    }

    static void ApplyVolumeAndCamera()
    {
        ApplyVolume();

        var cm = Object.FindFirstObjectByType<CinemachineCamera>();
        var lens = cm.Lens;
        lens.FieldOfView = 42f;
        cm.Lens = lens;
        PrefabUtility.RecordPrefabInstancePropertyModifications(cm);
        var follow = cm.GetComponent<CinemachineFollow>();
        follow.FollowOffset = new Vector3(0, 12.5f, -11.5f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
    }

    // Post volume and camera post-processing only; leaves the gameplay framing alone.
    static void ApplyVolume()
    {
        string path = Gen + "/WarmVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var tone = Comp<Tonemapping>(profile);
        tone.mode.value = TonemappingMode.Neutral;
        var adj = Comp<ColorAdjustments>(profile);
        adj.postExposure.value = 0.1f;
        adj.contrast.value = 10f;
        adj.saturation.value = 8f;
        adj.colorFilter.value = Color.white;
        adj.hueShift.value = 0;
        var wb = Comp<WhiteBalance>(profile);
        wb.temperature.value = 3f;
        wb.tint.value = 0f;
        var bloom = Comp<Bloom>(profile);
        bloom.threshold.value = 1.0f;
        bloom.intensity.value = 0.45f;
        bloom.scatter.value = 0.65f;
        bloom.tint.value = new Color(1f, 0.85f, 0.65f);
        bloom.highQualityFiltering.value = false;
        bloom.dirtTexture.overrideState = false;
        bloom.dirtIntensity.overrideState = false;
        var vig = Comp<Vignette>(profile);
        vig.color.value = new Color(0.30f, 0.15f, 0.10f);
        vig.center.value = new Vector2(0.5f, 0.5f);
        vig.intensity.value = 0.2f;
        vig.smoothness.value = 0.5f;
        vig.rounded.value = false;
        EditorUtility.SetDirty(profile);

        var volumeGo = GameObject.Find("StyleTest_Volume") ?? new GameObject("StyleTest_Volume");
        if (!volumeGo.TryGetComponent<Volume>(out var volume)) volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        var cam = Camera.main;
        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(camData);
        EditorUtility.SetDirty(camData);
    }

    // ---------- Capture ----------

    // Renders the gameplay camera to a PNG. offset is relative to the player; zero uses the rig's follow offset.
    // Same as Capture, but looks at a world point instead of the player.
    public static string CaptureAt(string file, Vector3 lookAt, Vector3 offset, int w = 1280, int h = 620)
    {
        overrideTarget = lookAt;
        try { return Capture(file, offset, w, h); }
        finally { overrideTarget = null; }
    }

    static Vector3? overrideTarget;

    public static string Capture(string file, Vector3 offset, int w = 1280, int h = 620)
    {
        var cam = Camera.main;
        var cm = Object.FindFirstObjectByType<CinemachineCamera>();
        var follow = cm.GetComponent<CinemachineFollow>();
        var target = cm.Follow != null ? cm.Follow.position : cam.transform.root.position;
        if (overrideTarget.HasValue) target = overrideTarget.Value;
        Vector3 usedOffset = offset == Vector3.zero ? follow.FollowOffset : offset;

        var pos = cam.transform.position;
        var rot = cam.transform.rotation;
        float fov = cam.fieldOfView;
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32); bool asyncShaders = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        try
        {
            cam.transform.position = target + usedOffset;
            cam.transform.LookAt(target + Vector3.up * 0.8f);
            cam.fieldOfView = cm.Lens.FieldOfView;
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(cam, req); RenderPipeline.SubmitRenderRequest(cam, req);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncShaders; cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
        }
        return file;
    }
}
