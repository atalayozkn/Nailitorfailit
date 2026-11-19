using UnityEngine;

// Extra overlay materials (wet, cracks) appended to a tile's renderers as additional material slots.
// Several overlays can sit on one renderer, so slots are found by material instead of assuming the last one, and the
// per-slot property blocks of the slots after a removed one are moved down with them.
public static class OverlaySlots
{
    private static readonly MaterialPropertyBlock moveBlock = new();

    public static int IndexOf(Renderer renderer, Material overlay)
    {
        Material[] current = renderer.sharedMaterials;
        for (int i = current.Length - 1; i >= 0; i--)
            if (current[i] == overlay) return i;
        return -1;
    }

    // Returns the slot index of the overlay (added at the end when missing).
    public static int Add(Renderer renderer, Material overlay)
    {
        int existing = IndexOf(renderer, overlay);
        if (existing >= 0) return existing;

        Material[] current = renderer.sharedMaterials;
        Material[] materials = new Material[current.Length + 1];
        current.CopyTo(materials, 0);
        materials[current.Length] = overlay;
        renderer.sharedMaterials = materials;
        return current.Length;
    }

    public static void Remove(Renderer renderer, Material overlay)
    {
        int index = IndexOf(renderer, overlay);
        if (index < 0) return;

        Material[] current = renderer.sharedMaterials;
        // Later slots move down one place: carry their property blocks along.
        for (int i = index; i < current.Length - 1; i++)
        {
            renderer.GetPropertyBlock(moveBlock, i + 1);
            renderer.SetPropertyBlock(moveBlock.isEmpty ? null : moveBlock, i);
        }
        renderer.SetPropertyBlock(null, current.Length - 1);

        Material[] materials = new Material[current.Length - 1];
        for (int i = 0, j = 0; i < current.Length; i++)
            if (i != index) materials[j++] = current[i];
        renderer.sharedMaterials = materials;
    }
}
