using Mirror;
using UnityEngine;

public static class MpPrefabResolver
{
    public static GameObject Resolve(GameObject source)
    {
        if (source == null) return null;

        NetworkManager manager = NetworkManager.singleton;
        if (manager == null) return source;

        string wanted = source.name + "_MP";

        foreach (GameObject prefab in manager.spawnPrefabs)
        {
            if (prefab == null) continue;
            if (prefab.name == wanted) return prefab;
        }

        return source;
    }

    public static bool IsMpVariant(GameObject source, out GameObject variant)
    {
        variant = Resolve(source);
        return variant != null && variant != source;
    }
}
