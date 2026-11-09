using System.Collections.Generic;
using UnityEngine;

// Marks meshes that have no collider (roofs) so CameraDitherHelper still dithers them when they
// hide the player. Collider-less on purpose: a roof collider would add physics nobody needs.
public class DitherOccluder : MonoBehaviour
{
    private static readonly HashSet<MeshRenderer> renderers = new();
    public static IReadOnlyCollection<MeshRenderer> Renderers => renderers;

    private MeshRenderer[] own;

    private void OnEnable()
    {
        own ??= GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in own) if (r != null) renderers.Add(r);
    }

    private void OnDisable()
    {
        if (own == null) return;
        foreach (var r in own) renderers.Remove(r);
    }
}
