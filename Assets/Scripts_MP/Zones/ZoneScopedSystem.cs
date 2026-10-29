using PlayerScripts;
using UnityEngine;

public abstract class ZoneScopedSystem : MonoBehaviour
{
    [SerializeField] private string zoneIdOverride = "";
    [SerializeField] private ZoneKind expectedKind = ZoneKind.SystemZone;

    public ZoneVolume Zone { get; private set; }

    protected virtual void OnEnable()
    {
        ResolveZone();

        if (ZoneSystem_MP.Instance != null)
        {
            ZoneSystem_MP.Instance.PlayerEnteredZone += HandlePlayerEntered;
            ZoneSystem_MP.Instance.PlayerExitedZone += HandlePlayerExited;
        }
    }

    protected virtual void OnDisable()
    {
        if (ZoneSystem_MP.Instance != null)
        {
            ZoneSystem_MP.Instance.PlayerEnteredZone -= HandlePlayerEntered;
            ZoneSystem_MP.Instance.PlayerExitedZone -= HandlePlayerExited;
        }
    }

    private void ResolveZone()
    {
        Zone = GetComponentInParent<ZoneVolume>();

        if (Zone == null && !string.IsNullOrWhiteSpace(zoneIdOverride))
            Zone = ZoneRegistry.Find(zoneIdOverride, expectedKind);
    }

    private void HandlePlayerEntered(PlayerNetwork_MP player, ZoneVolume zone)
    {
        if (zone != Zone) return;
        OnPlayerEntered(player);
    }

    private void HandlePlayerExited(PlayerNetwork_MP player, ZoneVolume zone)
    {
        if (zone != Zone) return;
        OnPlayerExited(player);
    }

    protected virtual void OnPlayerEntered(PlayerNetwork_MP player) { }
    protected virtual void OnPlayerExited(PlayerNetwork_MP player) { }
}
