#if UNITY_EDITOR
using System.IO;
using Mirror;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MpNetworkedCarPrefabBuilder
{
    private const string TargetFolder = "Assets/Prefabs_MP/Cars/Resources";
    private const string TargetPath = TargetFolder + "/Car_Active_MP.prefab";
    private const string StackPrefabPath = "Assets/Prefabs_MP/MpStack.prefab";

    static MpNetworkedCarPrefabBuilder()
    {
        EditorApplication.delayCall += AutoBuild;
    }

    private static void AutoBuild()
    {
        if (File.Exists(TargetPath)) return;
        Build();
    }

    [MenuItem("Tools/MP/Build Networked Car Prefab")]
    public static void Build()
    {
        string sourcePath = FindSourcePrefab();

        if (sourcePath == null)
        {
            Debug.LogError("[MP-CAR] source Car_Active prefab not found - the networked car was not built.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            Directory.CreateDirectory(TargetFolder);
            AssetDatabase.Refresh();
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = "Car_Active_MP";

        int playerMask = TakeOverHitLogic(instance);
        EnsureRigidbody(instance);
        EnsureNetworkIdentity(instance);
        EnsureNetworkTransform(instance);
        EnsureHitSync(instance, playerMask);

        PrefabUtility.SaveAsPrefabAsset(instance, TargetPath);
        Object.DestroyImmediate(instance);

        AssetDatabase.ImportAsset(TargetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireManager();

        Debug.Log($"[MP-CAR] built '{TargetPath}' from '{sourcePath}' (playerMask={playerMask}).");
    }

    private static int TakeOverHitLogic(GameObject root)
    {
        if (!root.TryGetComponent(out Car_Active car)) return 0;

        SerializedObject serialized = new SerializedObject(car);
        SerializedProperty mask = serialized.FindProperty("whatIsPlayer");
        if (mask == null) return 0;

        int value = mask.intValue;

        mask.intValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return value;
    }

    private static void EnsureRigidbody(GameObject root)
    {
        if (root.TryGetComponent(out Rigidbody body)) return;

        body = root.AddComponent<Rigidbody>();
        body.mass = 800f;
    }

    private static void EnsureNetworkIdentity(GameObject root)
    {
        if (root.TryGetComponent(out NetworkIdentity _)) return;

        root.AddComponent<NetworkIdentity>(); // server authority: no client authority flag set
    }

    private static void EnsureNetworkTransform(GameObject root)
    {
        if (root.TryGetComponent(out NetworkTransformReliable _)) return;

        NetworkTransformReliable transform = root.AddComponent<NetworkTransformReliable>();
        transform.syncDirection = SyncDirection.ServerToClient;
        transform.syncScale = false;
        transform.interpolatePosition = true;
        transform.interpolateRotation = true;
    }

    private static void EnsureHitSync(GameObject root, int playerMask)
    {
        if (!root.TryGetComponent(out MpCarHitSync_MP hit))
            hit = root.AddComponent<MpCarHitSync_MP>();

        SerializedObject serialized = new SerializedObject(hit);
        SerializedProperty mask = serialized.FindProperty("whatIsPlayer");

        if (mask != null)
        {
            mask.intValue = playerMask;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void WireManager()
    {
        GameObject stack = AssetDatabase.LoadAssetAtPath<GameObject>(StackPrefabPath);

        if (stack == null)
        {
            Debug.LogWarning($"[MP-CAR] '{StackPrefabPath}' not found - assign the car prefab to " +
                             "LobbyNetworkManager.trafficCarPrefab by hand.");
            return;
        }

        LobbyNetworkManager manager = stack.GetComponent<LobbyNetworkManager>();

        if (manager == null)
        {
            Debug.LogWarning($"[MP-CAR] no LobbyNetworkManager on '{StackPrefabPath}' - assign the car " +
                             "prefab to trafficCarPrefab by hand.");
            return;
        }

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty prefab = serialized.FindProperty("trafficCarPrefab");

        if (prefab == null)
        {
            Debug.LogWarning("[MP-CAR] LobbyNetworkManager has no 'trafficCarPrefab' field.");
            return;
        }

        if (prefab.objectReferenceValue != null) return;

        prefab.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[MP-CAR] LobbyNetworkManager.trafficCarPrefab wired to the networked car.");
    }

    private static string FindSourcePrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("Car_Active t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/Car_Active.prefab")) return path;
        }

        return null;
    }
}
#endif
