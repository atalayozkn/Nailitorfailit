using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Pulsing ground outline around a room's tiles, so the player sees which tile belongs to which room before building.
// Built at runtime by RoomController from its tile list, so no level needs hand-made art for it.
// Small rooms and big rooms get different colours (small rooms are the ones to build first). Hidden once the room is done.
public class RoomOutline : MonoBehaviour
{
    const string MaterialPath = "RoomOutline/RoomOutline";
    const int SmallRoomMaxTiles = 9;

    static readonly Color SmallRoomColor = new Color(0.45f, 0.85f, 0.5f, 0.9f);
    static readonly Color BigRoomColor = new Color(0.95f, 0.66f, 0.32f, 0.9f);
    const float LineWidth = 0.09f;
    const float Inset = 0.14f;      // pulled inside the room edge so two neighbouring rooms show two separate lines
    const float Lift = 0.03f;       // above the tile tops so built tiles do not cover it
    const float SlabReach = 0.5f;   // only meshes that start this close to the tile base count as the floor slab
    const float MaxAboveBase = 1f;  // hard cap: the line never floats more than this over the tile base

    static Material sharedMaterial;
    private LineRenderer line;

    public static RoomOutline Create(RoomController room, GameObject[] tiles)
    {
        if (tiles == null || tiles.Length == 0) return null;
        if (sharedMaterial == null) sharedMaterial = Resources.Load<Material>(MaterialPath);
        if (sharedMaterial == null)
        {
            Debug.LogWarning($"[RoomOutline] Missing Resources/{MaterialPath}.mat", room);
            return null;
        }

        // The room's own frame: RoomLayout root when there is one, so rotated rooms still get a tight rectangle.
        RoomLayout layout = room.GetComponentInParent<RoomLayout>(true);
        Transform frame = layout != null ? layout.transform : room.transform;

        var go = new GameObject("__RoomOutline");
        go.transform.SetParent(room.transform, false);
        var outline = go.AddComponent<RoomOutline>();
        if (!outline.Build(frame, tiles))
        {
            Destroy(go);
            return null;
        }
        return outline;
    }

    public void Hide() => gameObject.SetActive(false);

    private bool Build(Transform frame, GameObject[] tiles)
    {
        var points = new List<Vector3>();
        float top = float.MinValue;
        foreach (GameObject tile in tiles)
        {
            if (tile == null) continue;
            points.Add(frame.InverseTransformPoint(tile.transform.position));
            top = Mathf.Max(top, TileTop(tile));
        }
        if (points.Count == 0) return false;

        float step = TileSpacing(points);
        float half = step * 0.5f - Inset;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 p in points)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
        }

        Vector3[] corners =
        {
            new Vector3(minX - half, 0f, minZ - half),
            new Vector3(maxX + half, 0f, minZ - half),
            new Vector3(maxX + half, 0f, maxZ + half),
            new Vector3(minX - half, 0f, maxZ + half),
        };
        // Rooms only turn around Y, so the corners keep one flat height just above the highest tile.
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 world = frame.TransformPoint(corners[i]);
            corners[i] = new Vector3(world.x, top + Lift, world.z);
        }

        // Lies flat: TransformZ alignment faces the line along this object's forward, which points straight down.
        transform.rotation = Quaternion.LookRotation(Vector3.down, frame.forward);

        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = corners.Length;
        line.SetPositions(corners);
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Tile;
        line.widthMultiplier = LineWidth;
        line.numCornerVertices = 2;
        line.sharedMaterial = sharedMaterial;
        Color color = points.Count <= SmallRoomMaxTiles ? SmallRoomColor : BigRoomColor;
        line.startColor = line.endColor = color;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return true;
    }

    // Smallest gap between two tiles = one tile. Falls back to 1 m for a single-tile room.
    private static float TileSpacing(List<Vector3> points)
    {
        float best = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                Vector3 d = points[i] - points[j];
                d.y = 0f;
                float m = d.magnitude;
                if (m > 0.05f && m < best) best = m;
            }
        return best == float.MaxValue ? 1f : best;
    }

    // Top of the tile's floor slab (all variants, built or not). Mesh bounds work on hidden objects too.
    // Tiles also carry helper meshes (hammer icon, puddle, VFX, world canvas, walls) that reach far above the floor
    // and lifted the line to around +15. Only meshes whose bottom starts near the tile base count, and the result is
    // capped so the outline always hugs the room's floor.
    private static float TileTop(GameObject tile)
    {
        float baseY = tile.transform.position.y;
        float top = baseY;
        foreach (MeshFilter filter in tile.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.name.StartsWith("__")) continue;
            if (filter.GetComponentInParent<Canvas>(true) != null) continue;
            Bounds b = filter.sharedMesh.bounds;
            Vector3 c = b.center, e = b.extents;
            float meshMin = float.MaxValue, meshMax = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                Vector3 corner = c + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                float y = filter.transform.TransformPoint(corner).y;
                meshMin = Mathf.Min(meshMin, y);
                meshMax = Mathf.Max(meshMax, y);
            }
            if (Mathf.Abs(meshMin - baseY) > SlabReach) continue;
            top = Mathf.Max(top, meshMax);
        }
        return Mathf.Min(top, baseY + MaxAboveBase);
    }
}
