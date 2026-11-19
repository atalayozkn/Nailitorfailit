using UnityEngine;

// Loads the Resources assets that systems otherwise fetch lazily on first use (the first VFX, hover glow,
// crack, room outline, x-ray or dog icon). Loading them mid-level froze the game for up to ~90 ms on the
// first interaction; here it happens once at boot, behind the boot screen. Holding the references stops
// scene changes from unloading them again. Paths must match the constants in each system.
public static class AssetWarmup
{
    private static readonly string[] Paths =
    {
        "VfxLibrary",
        "Highlight/HoverGlow",
        "Highlight/XRaySilhouette",
        "Cracks/Crack_Wood",
        "Cracks/Crack_Glass",
        "RoomOutline/RoomOutline",
    };

    private const string DogIconFolder = "DogIcons";

    private static Object[] held;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Warm()
    {
        if (held != null) return;

        Object[] dogIcons = Resources.LoadAll<Sprite>(DogIconFolder);
        held = new Object[Paths.Length + dogIcons.Length];
        for (int i = 0; i < Paths.Length; i++)
        {
            held[i] = Resources.Load(Paths[i]);
            if (held[i] == null) Debug.LogWarning("[AssetWarmup] missing Resources/" + Paths[i]);
        }
        dogIcons.CopyTo(held, Paths.Length);
    }
}
