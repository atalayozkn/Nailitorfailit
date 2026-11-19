using PlayerScripts;
using UnityEngine;

// The playable characters of multiplayer. Entry 0 is the model the player prefab ships with (nothing is swapped); the
// others are the extra body models, re-skinned to the same Mixamo skeleton by the editor tool "NIOFI > MP > Bake
// characters" (so every animation, the ragdoll and the remote-bone sync work unchanged) and stored in the asset
// Resources/MpCharacterSet_MP. Switching a character only swaps the renderer's mesh (and material); the skeleton stays.
public class MpCharacterSet_MP : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string displayName;
        public Mesh mesh;         // null = the prefab's own mesh (entry 0)
        public Material material; // null = the prefab's own material
    }

    public const string ResourceName = "MpCharacterSet_MP";

    public Entry[] entries;

    private static MpCharacterSet_MP cached;

    public static MpCharacterSet_MP Get()
    {
        if (cached == null) cached = Resources.Load<MpCharacterSet_MP>(ResourceName);
        return cached;
    }

    public static int Count
    {
        get
        {
            MpCharacterSet_MP set = Get();
            return set != null && set.entries != null && set.entries.Length > 0 ? set.entries.Length : 1;
        }
    }

    public static int Clamp(int index) => index < 0 || index >= Count ? 0 : index;

    public static string NameOf(int index)
    {
        MpCharacterSet_MP set = Get();
        index = Clamp(index);
        return set != null && set.entries != null && index < set.entries.Length && !string.IsNullOrEmpty(set.entries[index].displayName)
            ? set.entries[index].displayName
            : "Man";
    }

    // Puts the chosen body on the player (call before MpPlayerTint_MP.Apply: the tint works on the material set here).
    public static void Apply(PlayerNetwork_MP player)
    {
        if (player == null) return;

        MpCharacterSet_MP set = Get();
        int index = Clamp(player.characterIndex);

        foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.transform.parent == null || renderer.transform.parent.name != "Character_01_Male") continue;

            var state = renderer.GetComponent<MpCharacterState_MP>();
            if (state == null)
            {
                state = renderer.gameObject.AddComponent<MpCharacterState_MP>();
                state.originalMesh = renderer.sharedMesh;
                state.originalMaterial = renderer.sharedMaterial;
            }

            if (state.appliedIndex == index) continue;
            state.appliedIndex = index;

            Entry entry = set != null && set.entries != null && index < set.entries.Length ? set.entries[index] : null;
            Mesh mesh = entry != null && entry.mesh != null ? entry.mesh : state.originalMesh;
            Material material = entry != null && entry.material != null ? entry.material : state.originalMaterial;

            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material; // fresh shared material: the colour tint rebuilds its copy afterwards
        }
    }
}

// Remembers what the body renderer shipped with, so entry 0 can be restored.
public class MpCharacterState_MP : MonoBehaviour
{
    public Mesh originalMesh;
    public Material originalMaterial;
    public int appliedIndex = -1;
}
