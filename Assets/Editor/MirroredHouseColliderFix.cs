using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-shot tool for Level_01. Box colliders don't support negative scale, and every child of a
// mirrored house inherits it, so each replacement sits next to the house instead of inside it.
public static class MirroredHouseColliderFix
{
    const string SceneName = "Level_01";
    const string HouseGroupName = "evler";
    const string BackupParent = "Assets/Scenes/Level_01";
    const string BackupFolderName = "_ColliderBackup";

    [MenuItem("Tools/Level_01/Replace Mirrored House Colliders")]
    static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogError($"[ColliderFix] Open {SceneName} first. Active scene: {scene.name}");
            return;
        }

        // The backup copies the scene file, so unsaved work would be missing from it.
        if (scene.isDirty)
        {
            Debug.LogError("[ColliderFix] Save the scene first, then run this again.");
            return;
        }

        List<BoxCollider> colliders = FindMirroredColliders();
        if (colliders.Count == 0)
        {
            Debug.LogWarning("[ColliderFix] No mirrored house box colliders found, nothing changed.");
            return;
        }

        string message = $"Mirrored house colliders found: {colliders.Count}\n\n" +
                         "Turns each one off and adds a matching box next to the house. A scene backup is made first.";
        if (!EditorUtility.DisplayDialog("Replace Mirrored House Colliders", message, "Replace", "Cancel"))
            return;

        BackupScene(scene);

        foreach (BoxCollider collider in colliders)
            Replace(collider);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[ColliderFix] Replaced {colliders.Count} colliders. Save the scene.");
    }

    static List<BoxCollider> FindMirroredColliders()
    {
        var result = new List<BoxCollider>();
        foreach (BoxCollider collider in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
        {
            if (!collider.enabled || !IsUnderHouseGroup(collider.transform))
                continue;

            Vector3 scale = collider.transform.lossyScale;
            if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
                result.Add(collider);
        }
        return result;
    }

    static bool IsUnderHouseGroup(Transform transform)
    {
        for (Transform current = transform.parent; current != null; current = current.parent)
        {
            if (current.name == HouseGroupName)
                return true;
        }
        return false;
    }

    static void Replace(BoxCollider source)
    {
        Transform sourceTransform = source.transform;
        Matrix4x4 matrix = sourceTransform.localToWorldMatrix;

        // Reading the matrix axes stays correct under mirroring and rotated parents.
        Vector3 axisX = matrix.GetColumn(0);
        Vector3 axisY = matrix.GetColumn(1);
        Vector3 axisZ = matrix.GetColumn(2);

        if (Mathf.Abs(Vector3.Dot(axisX.normalized, axisY.normalized)) > 0.01f ||
            Mathf.Abs(Vector3.Dot(axisY.normalized, axisZ.normalized)) > 0.01f)
        {
            Debug.LogWarning($"[ColliderFix] {sourceTransform.name} is skewed by its parents, so its new box is approximate.", source);
        }

        Vector3 worldCenter = sourceTransform.TransformPoint(source.center);
        Quaternion worldRotation = Quaternion.LookRotation(axisZ, axisY);
        var worldSize = new Vector3(
            axisX.magnitude * Mathf.Abs(source.size.x),
            axisY.magnitude * Mathf.Abs(source.size.y),
            axisZ.magnitude * Mathf.Abs(source.size.z));

        GameObject house = PrefabUtility.GetOutermostPrefabInstanceRoot(source.gameObject);
        if (house == null)
            house = sourceTransform.gameObject;
        Transform parent = house.transform.parent;

        var replacement = new GameObject($"{house.name} Collider");
        Undo.RegisterCreatedObjectUndo(replacement, "Replace Mirrored House Collider");

        replacement.layer = source.gameObject.layer;
        replacement.tag = source.gameObject.tag;
        GameObjectUtility.SetStaticEditorFlags(replacement, GameObjectUtility.GetStaticEditorFlags(source.gameObject));

        Transform replacementTransform = replacement.transform;
        replacementTransform.SetParent(parent, false);
        replacementTransform.SetPositionAndRotation(worldCenter, worldRotation);

        // Keeps the box at its measured world size if the parent group is scaled.
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        replacementTransform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

        var box = replacement.AddComponent<BoxCollider>();
        box.size = worldSize;
        box.sharedMaterial = source.sharedMaterial;

        Undo.RecordObject(source, "Replace Mirrored House Collider");
        source.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(source);

        Debug.Log($"[ColliderFix] {house.name}: center={worldCenter}, size={worldSize}", replacement);
    }

    static void BackupScene(Scene scene)
    {
        string folder = $"{BackupParent}/{BackupFolderName}";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(BackupParent, BackupFolderName);

        string destination = $"{folder}/{scene.name}_before_colliders.unity";
        if (AssetDatabase.LoadMainAssetAtPath(destination) == null && !AssetDatabase.CopyAsset(scene.path, destination))
            Debug.LogError($"[ColliderFix] Backup failed: {destination}");
    }
}
