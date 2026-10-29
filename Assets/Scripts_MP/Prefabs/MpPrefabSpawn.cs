using Mirror;
using UnityEngine;

public static class MpPrefabSpawn
{
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        bool networked = NetworkServer.active || NetworkClient.active;

        GameObject resolved = networked ? MpPrefabResolver.Resolve(prefab) : prefab;
        GameObject instance = Object.Instantiate(resolved, position, rotation);

        if (instance == null) return null;

        if (networked && NetworkServer.active &&
            instance.TryGetComponent(out NetworkIdentity identity) && identity.netId == 0)
        {
            NetworkServer.Spawn(instance);
        }

        return instance;
    }

    public static GameObject Spawn(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;

        bool networked = NetworkServer.active || NetworkClient.active;

        GameObject resolved = networked ? MpPrefabResolver.Resolve(prefab) : prefab;
        GameObject instance = Object.Instantiate(resolved, parent);

        if (instance == null) return null;

        if (networked && NetworkServer.active &&
            instance.TryGetComponent(out NetworkIdentity identity) && identity.netId == 0)
        {
            NetworkServer.Spawn(instance);
        }

        return instance;
    }
}
