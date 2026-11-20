using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Paints terrain details (grass, plants) only where the terrain is actually exposed, inside a radius around a
// centre object such as the house. Every detail cell in the circle is probed from above: if the first thing a
// downward sphere cast hits is the terrain itself, the cell is bare ground and gets painted; if it hits a road,
// sidewalk, building, court or prop first, it is skipped.
// Renderers without colliders (some floor meshes) are given temporary mesh colliders for the scan, so they also
// count as cover. Undo works (Ctrl+Z).
public class TerrainDetailAutoPaint : EditorWindow
{
    [System.Serializable]
    private class DetailEntry
    {
        public bool enabled = true;
        public GameObject prefab;
        public Texture2D texture;
        [Range(0f, 1f)] public float amount = 0.45f;
        [Range(0f, 1f)] public float fill = 1f;
        public Vector2 width = new(0.25f, 0.5f);
        public Vector2 height = new(0.25f, 0.5f);
    }

    [SerializeField] private Terrain terrain;
    [SerializeField] private Transform center;
    [SerializeField] private float radius = 60f;
    [SerializeField] private float edgeMargin = 0.35f;
    [SerializeField] private bool clearFirst = true;
    [SerializeField] private float clumpScale = 0.12f;
    [SerializeField, Range(0f, 1f)] private float clumpStrength = 0.5f;
    [SerializeField] private int seed = 1;
    [SerializeField] private LayerMask coverLayers = ~0;
    [SerializeField] private string ignoreNames = "Boundary,Catcher,Trigger";
    [SerializeField] private bool tempCollidersForRenderers = true;
    [SerializeField] private TerrainData importFrom;
    [SerializeField] private List<DetailEntry> details = new();

    private Vector2 scroll;
    private SerializedObject so;

    [MenuItem("Tools/NailIt/Terrain Detail Auto Paint")]
    private static void Open() => GetWindow<TerrainDetailAutoPaint>("Detail Auto Paint");

    private void OnEnable()
    {
        so = new SerializedObject(this);
        if (terrain == null) terrain = Terrain.activeTerrain;
        // Player, UI, interaction volumes and ignore-raycast never count as cover.
        if (coverLayers == ~0)
            coverLayers = ~LayerMask.GetMask("Ignore Raycast", "UI", "Player", "Interaction", "Dog", "MailMan", "CarriableContact", "TransparentFX");
        SceneView.duringSceneGui += DrawRadius;
    }

    private void OnDisable() => SceneView.duringSceneGui -= DrawRadius;

    private void DrawRadius(SceneView view)
    {
        if (center == null) return;
        Handles.color = new Color(0.4f, 1f, 0.4f, 0.9f);
        Handles.DrawWireDisc(center.position, Vector3.up, radius, 2f);
        EditorGUI.BeginChangeCheck();
        float r = Handles.RadiusHandle(Quaternion.identity, center.position, radius);
        if (EditorGUI.EndChangeCheck()) { radius = r; Repaint(); }
    }

    private void OnGUI()
    {
        so.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty(nameof(terrain)));
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(so.FindProperty(nameof(center)));
            if (GUILayout.Button("Use Selection", GUILayout.Width(100)) && Selection.activeTransform != null)
                so.FindProperty(nameof(center)).objectReferenceValue = Selection.activeTransform;
        }
        EditorGUILayout.PropertyField(so.FindProperty(nameof(radius)));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("What counts as covered", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty(nameof(coverLayers)));
        EditorGUILayout.PropertyField(so.FindProperty(nameof(edgeMargin)), new GUIContent("Edge Margin (m)", "Keeps grass this far from roads, walls and props."));
        EditorGUILayout.PropertyField(so.FindProperty(nameof(ignoreNames)), new GUIContent("Ignore Names", "Comma separated. Colliders whose object name contains one of these are not cover."));
        EditorGUILayout.PropertyField(so.FindProperty(nameof(tempCollidersForRenderers)), new GUIContent("Renderers Count As Cover", "Meshes without colliders (some floors) get a temporary collider during the scan."));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty(nameof(clearFirst)), new GUIContent("Clear Inside Radius First"));
        EditorGUILayout.PropertyField(so.FindProperty(nameof(clumpScale)));
        EditorGUILayout.PropertyField(so.FindProperty(nameof(clumpStrength)));
        EditorGUILayout.PropertyField(so.FindProperty(nameof(seed)));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(so.FindProperty(nameof(importFrom)), new GUIContent("Import From"));
            if (GUILayout.Button("Import", GUILayout.Width(60))) { so.ApplyModifiedProperties(); ImportFrom(importFrom); so.Update(); }
        }
        if (terrain != null && GUILayout.Button("Import From Current Terrain")) { so.ApplyModifiedProperties(); ImportFrom(terrain.terrainData); so.Update(); }
        EditorGUILayout.PropertyField(so.FindProperty(nameof(details)), true);
        EditorGUILayout.HelpBox("Amount = how thick the detail is where it grows (coverage). Fill = share of exposed cells that get it at all.", MessageType.None);

        EditorGUILayout.EndScrollView();
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(terrain == null || center == null))
        {
            if (GUILayout.Button("Paint Exposed Ground", GUILayout.Height(30))) Debug.Log("[DetailAutoPaint] " + Paint(false));
            if (GUILayout.Button("Clear Details Inside Radius")) Debug.Log("[DetailAutoPaint] " + Paint(true));
        }
    }

    private void ImportFrom(TerrainData source)
    {
        if (source == null) return;
        details.Clear();
        foreach (var p in source.detailPrototypes)
        {
            details.Add(new DetailEntry
            {
                prefab = p.usePrototypeMesh ? p.prototype : null,
                texture = p.usePrototypeMesh ? null : p.prototypeTexture,
                amount = Mathf.Clamp01(p.targetCoverage > 0 ? p.targetCoverage : 0.45f),
                width = new Vector2(p.minWidth, p.maxWidth),
                height = new Vector2(p.minHeight, p.maxHeight),
            });
        }
    }

    // Makes sure the terrain has a prototype for each entry and returns their indices (-1 when disabled/empty).
    private int[] EnsurePrototypes(TerrainData data)
    {
        var protos = new List<DetailPrototype>(data.detailPrototypes);
        var map = new int[details.Count];
        for (int i = 0; i < details.Count; i++)
        {
            var e = details[i];
            map[i] = -1;
            if (!e.enabled || (e.prefab == null && e.texture == null)) continue;
            int found = protos.FindIndex(p => e.prefab != null ? p.usePrototypeMesh && p.prototype == e.prefab : !p.usePrototypeMesh && p.prototypeTexture == e.texture);
            if (found < 0)
            {
                var p = new DetailPrototype
                {
                    usePrototypeMesh = e.prefab != null,
                    prototype = e.prefab,
                    prototypeTexture = e.texture,
                    renderMode = e.prefab != null ? DetailRenderMode.VertexLit : DetailRenderMode.GrassBillboard,
                    useInstancing = e.prefab != null,
                };
                protos.Add(p);
                found = protos.Count - 1;
            }
            var proto = protos[found];
            proto.minWidth = e.width.x; proto.maxWidth = e.width.y;
            proto.minHeight = e.height.x; proto.maxHeight = e.height.y;
            proto.targetCoverage = 1f;
            proto.density = 1f;
            proto.noiseSpread = 0.1f;
            map[i] = found;
        }
        data.detailPrototypes = protos.ToArray();
        data.RefreshPrototypes();
        return map;
    }

    private string Paint(bool clearOnly)
    {
        var data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, clearOnly ? "Clear Terrain Details" : "Auto Paint Terrain Details");

        int[] protoIndex = clearOnly ? null : EnsurePrototypes(data);
        int res = data.detailResolution;
        Vector3 tPos = terrain.transform.position;
        Vector3 size = data.size;
        float cell = size.x / res;
        Vector3 c = center.position;

        int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - radius - tPos.x) / size.x * res), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + radius - tPos.x) / size.x * res), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - radius - tPos.z) / size.z * res), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + radius - tPos.z) / size.z * res), 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;

        int layerCount = data.detailPrototypes.Length;
        var layers = new int[layerCount][,];
        for (int l = 0; l < layerCount; l++) layers[l] = data.GetDetailLayer(x0, z0, w, h, l);

        bool coverage = data.detailScatterMode == DetailScatterMode.CoverageMode;
        int maxValue = coverage ? 255 : 16;
        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        string[] ignore = ignoreNames.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        var temps = new List<MeshCollider>();
        int exposed = 0, covered = 0;
        var rng = new System.Random(seed);

        try
        {
            if (!clearOnly && tempCollidersForRenderers) AddTempColliders(c, temps);
            Physics.SyncTransforms();
            float top = tPos.y + size.y + 20f;

            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                // Detail cell centre in world space. GetDetailLayer is indexed [y, x].
                float wx = tPos.x + (x0 + x + 0.5f) * cell;
                float wz = tPos.z + (z0 + z + 0.5f) * (size.z / res);
                float dx = wx - c.x, dz = wz - c.z;
                if (dx * dx + dz * dz > radius * radius) continue;

                if (clearFirst || clearOnly)
                    for (int l = 0; l < layerCount; l++) layers[l][z, x] = 0;
                if (clearOnly) continue;

                if (!IsExposed(new Vector3(wx, top, wz), terrainCollider, ignore)) { covered++; continue; }
                exposed++;

                float clump = Mathf.Lerp(1f, Mathf.PerlinNoise(wx * clumpScale + seed * 13.1f, wz * clumpScale + seed * 7.7f) * 2f, clumpStrength);
                for (int i = 0; i < details.Count; i++)
                {
                    int l = protoIndex[i];
                    if (l < 0) continue;
                    var e = details[i];
                    if (rng.NextDouble() > e.fill * Mathf.Clamp01(clump)) continue;
                    int value = Mathf.Clamp(Mathf.RoundToInt(e.amount * Mathf.Clamp(clump, 0.3f, 1.5f) * maxValue), 0, maxValue);
                    layers[l][z, x] = Mathf.Max(layers[l][z, x], value);
                }
            }
        }
        finally
        {
            foreach (var t in temps) if (t != null) DestroyImmediate(t);
            Physics.SyncTransforms();
        }

        for (int l = 0; l < layerCount; l++) data.SetDetailLayer(x0, z0, l, layers[l]);
        EditorUtility.SetDirty(data);
        return clearOnly ? $"Cleared details within {radius} m of {center.name}."
                         : $"Painted {exposed} exposed cells, skipped {covered} covered cells within {radius} m of {center.name} ({temps.Count} temp colliders).";
    }

    private bool IsExposed(Vector3 from, TerrainCollider terrainCollider, string[] ignore)
    {
        float dist = from.y + 100f;
        var hits = edgeMargin > 0f
            ? Physics.SphereCastAll(from, edgeMargin, Vector3.down, dist, coverLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(from, Vector3.down, dist, coverLayers, QueryTriggerInteraction.Ignore);
        // The terrain must be in the column, and only things met before it (above the ground) count as cover.
        // Colliders under the surface, like a flat physics floor sunk just below the terrain, do not.
        float terrainDistance = float.MaxValue;
        foreach (var hit in hits)
            if (hit.collider == terrainCollider) terrainDistance = Mathf.Min(terrainDistance, hit.distance);
        if (terrainDistance == float.MaxValue) return false;

        foreach (var hit in hits)
        {
            if (hit.collider == terrainCollider || IsIgnored(hit.collider.name, ignore)) continue;
            if (hit.distance < terrainDistance - 0.02f) return false; // distance 0 = sphere started inside it, also cover
        }
        return true;
    }

    private static bool IsIgnored(string name, string[] ignore)
    {
        foreach (var s in ignore)
            if (name.IndexOf(s.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private void AddTempColliders(Vector3 c, List<MeshCollider> temps)
    {
        float reach = radius + 10f;
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            // Moving things (cars, carriables) are not ground cover, and a concave collider on a rigidbody errors.
            if (!r.enabled || r.GetComponent<Collider>() != null || r.GetComponentInParent<Rigidbody>() != null) continue;
            if (((1 << r.gameObject.layer) & coverLayers) == 0) continue;
            var b = r.bounds;
            if (Mathf.Abs(b.center.x - c.x) > reach + b.extents.x || Mathf.Abs(b.center.z - c.z) > reach + b.extents.z) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>();
            if (mc == null) continue; // Unity refuses on some objects (2D physics, UI); they are not ground cover anyway
            mc.hideFlags = HideFlags.HideAndDontSave;
            mc.sharedMesh = mf.sharedMesh;
            temps.Add(mc);
        }
    }
}
