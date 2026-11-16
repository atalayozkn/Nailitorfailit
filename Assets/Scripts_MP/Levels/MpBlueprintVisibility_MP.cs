using System.Reflection;
using ItemScript;
using Mirror;
using UnityEngine;

// Build-area blueprints (the blue floor grids / wall spots of every Constructor): every spot that is not built yet
// always shows, with empty hands too, so players can see where the house will go. Built spots stay hidden.
// Set HideWhenUseless to bring back the old behaviour (only the spots that fit the carried material show).
// Multiplayer only: singleplayer keeps showing every blueprint.
public class MpBlueprintVisibility_MP : MonoBehaviour
{
    public static bool HideWhenUseless = false;

    private const float TickSeconds = 0.1f;
    private const float RescanSeconds = 2f;

    private static readonly FieldInfo BlueprintField =
        typeof(Constructor).GetField("bluePrintObject", BindingFlags.Instance | BindingFlags.NonPublic);

    private Constructor[] constructors = new Constructor[0];
    private float nextTickAt;
    private float nextScanAt;

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
        CarriableType heldType = hasMaterial ? held.carriableType : default;

        foreach (Constructor constructor in constructors)
        {
            if (constructor == null || BlueprintField == null) continue;

            var blueprint = BlueprintField.GetValue(constructor) as GameObject;
            if (blueprint == null) continue;

            bool show = !constructor.IsBuilt() && (!HideWhenUseless || (hasMaterial && constructor.GetAcceptedType() == heldType));
            if (blueprint.activeSelf != show) blueprint.SetActive(show);
        }
    }
}
