using System.Collections.Generic;
using UnityEngine;

public static class ZoneRegistry
{
    private static readonly List<ZoneVolume> zones = new List<ZoneVolume>();

    public static IReadOnlyList<ZoneVolume> All => zones;
    public static int Count => zones.Count;

    public static void Register(ZoneVolume zone)
    {
        if (zone == null || zones.Contains(zone)) return;
        zones.Add(zone);
    }

    public static void Unregister(ZoneVolume zone)
    {
        if (zone == null) return;
        zones.Remove(zone);
    }

    public static ZoneVolume Find(string id, ZoneKind? kind = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        foreach (ZoneVolume zone in zones)
        {
            if (zone == null) continue;
            if (kind.HasValue && zone.Kind != kind.Value) continue;
            if (zone.ZoneId == id) return zone;
        }

        return null;
    }

    public static ZoneVolume PrimaryPlayArea()
    {
        ZoneVolume fallback = null;

        foreach (ZoneVolume zone in zones)
        {
            if (zone == null || zone.Kind != ZoneKind.PlayArea) continue;
            if (zone.IsPrimaryPlayArea) return zone;
            if (fallback == null) fallback = zone;
        }

        return fallback;
    }

    public static ZoneVolume Nearest(Vector3 position, ZoneKind kind)
    {
        ZoneVolume best = null;
        float bestDistance = float.MaxValue;

        foreach (ZoneVolume zone in zones)
        {
            if (zone == null || zone.Kind != kind) continue;

            float d = (zone.Center - position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = zone;
            }
        }

        return best;
    }

    public static List<ZoneVolume> Containing(Vector3 position, ZoneKind? kind = null)
    {
        var result = new List<ZoneVolume>();

        foreach (ZoneVolume zone in zones)
        {
            if (zone == null) continue;
            if (kind.HasValue && zone.Kind != kind.Value) continue;
            if (zone.Contains(position)) result.Add(zone);
        }

        return result;
    }

    public static bool IsInside(string id, Vector3 position)
    {
        ZoneVolume zone = Find(id);
        return zone != null && zone.Contains(position);
    }
}
