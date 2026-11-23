using UnityEditor;
using UnityEngine;

// Tools > NailIt > Level > Add Play Area Border: drops the level's walkable-area box (the border players cannot leave in
// multiplayer) into the open scene, sized to the ground, ready to be dragged to the size you want.
public static class PlayAreaMenu
{
    [MenuItem("Tools/NailIt/Level/Add Play Area Border")]
    private static void Add()
    {
        foreach (ZoneVolume existing in Object.FindObjectsByType<ZoneVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (existing.Kind != ZoneKind.PlayArea || !existing.IsPrimaryPlayArea) continue;

            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log($"[PlayArea] '{existing.name}' is already the level's play area: select it and use the box collider's Edit Collider handles.");
            return;
        }

        int ground = LayerMask.NameToLayer("Ground");
        bool has = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (ground >= 0 && renderer.gameObject.layer != ground) continue;

            if (!has) { bounds = renderer.bounds; has = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        if (!has) bounds = new Bounds(Vector3.zero, new Vector3(60f, 20f, 60f));

        var go = new GameObject("Zone_PlayArea");
        Undo.RegisterCreatedObjectUndo(go, "Add Play Area Border");

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = bounds.center;
        box.size = new Vector3(bounds.size.x, Mathf.Max(20f, bounds.size.y), bounds.size.z);

        var zone = go.AddComponent<ZoneVolume>();
        zone.Configure(ZoneKind.PlayArea, true, BoundaryPolicy.PushBack, "play_area");

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("[PlayArea] Zone_PlayArea added. Edit the box (Edit Collider) to the border you want; Policy = PushBack.");
    }
}
