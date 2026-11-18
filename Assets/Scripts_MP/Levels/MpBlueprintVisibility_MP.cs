using System.Reflection;
using ItemScript;
using Mirror;
using UnityEngine;

// Build-area blueprints (the blue floor grids / wall spots of every Constructor): with empty hands (or the wrong
// material) every spot is only a faint hint so the player can always see where the house goes; holding a processed wood
// plank lights the wood spots up fully, a brick the brick spots, glass the glass spots. Purely local (each player sees
// what fits what THEY carry); built spots stay hidden as before. Multiplayer only: singleplayer is untouched.
public class MpBlueprintVisibility_MP : MonoBehaviour
{
    private const float TickSeconds = 0.1f;
    private const float FaintAlpha = 0.22f;
    private const float RescanSeconds = 2f;

    private static readonly FieldInfo BlueprintField =
        typeof(Constructor).GetField("bluePrintObject", BindingFlags.Instance | BindingFlags.NonPublic);

    private Constructor[] constructors = new Constructor[0];
    private float nextTickAt;
    private float nextScanAt;
    private bool holdsMaterial;
    private CarriableType heldType;

    private void Awake()
    {
        BlueprintVisual.AlphaOverride = AlphaFor;
    }

    private void OnDestroy()
    {
        if (BlueprintVisual.AlphaOverride == (System.Func<BlueprintVisual, float>)AlphaFor) BlueprintVisual.AlphaOverride = null;
    }

    private float AlphaFor(BlueprintVisual visual)
    {
        bool fits = holdsMaterial && visual != null && visual.Owner != null && visual.Owner.GetAcceptedType() == heldType;
        return fits ? 1f : FaintAlpha;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpBlueprintVisibility_MP>(FindObjectsInactive.Include) != null) return;

        var host = new GameObject("MpBlueprintVisibility_MP");
        DontDestroyOnLoad(host);
        host.AddComponent<MpBlueprintVisibility_MP>();
    }

    private void Update()
    {
        if (!NetworkClient.active || NetworkClient.localPlayer == null) return;
        if (Time.unscaledTime < nextTickAt) return;
        nextTickAt = Time.unscaledTime + TickSeconds;

        PlayerInteractionHandler handler = NetworkClient.localPlayer.GetComponentInChildren<PlayerInteractionHandler>(true);
        if (handler == null) return; // still in the lobby

        if (Time.unscaledTime >= nextScanAt)
        {
            nextScanAt = Time.unscaledTime + RescanSeconds;
            constructors = FindObjectsByType<Constructor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        CarriableObject_SP held = handler.GetCurrentCarriable();
        bool hasMaterial = held != null && !held.isRawMaterial;
        holdsMaterial = hasMaterial;
        heldType = hasMaterial ? held.carriableType : default;

        foreach (Constructor constructor in constructors)
        {
            if (constructor == null || BlueprintField == null) continue;

            var blueprint = BlueprintField.GetValue(constructor) as GameObject;
            if (blueprint == null) continue;

            // Always drawn until built; how strongly is decided per slot in AlphaFor.
            bool show = !constructor.IsBuilt();
            if (blueprint.activeSelf != show) blueprint.SetActive(show);
        }
    }
}
