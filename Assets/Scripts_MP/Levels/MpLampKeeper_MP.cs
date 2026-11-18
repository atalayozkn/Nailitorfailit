using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the replaced street lamp meshes switched off. Something in the level (not found yet) brings an old lamp mesh back
// when the player walks up to it, so it showed next to the new post. Re-asserted every frame, before it is drawn.
public class MpLampKeeper_MP : MonoBehaviour
{
    private readonly List<MeshRenderer> old = new List<MeshRenderer>();

    public static MpLampKeeper_MP For(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "MpLampKeeper_MP" && root.TryGetComponent(out MpLampKeeper_MP existing)) return existing;

        var go = new GameObject("MpLampKeeper_MP");
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.AddComponent<MpLampKeeper_MP>();
    }

    public void Add(MeshRenderer renderer)
    {
        if (renderer != null && !old.Contains(renderer)) old.Add(renderer);
    }

    private void LateUpdate()
    {
        for (int i = 0; i < old.Count; i++)
            if (old[i] != null && old[i].enabled) old[i].enabled = false;
    }
}
