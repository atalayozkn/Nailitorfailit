using UnityEngine;

/// <summary>
/// A station that stores carriables in a visible spot (cutter slot, mixer slots)
/// exposes that spot here. The networked place operation (CarriableObject_MP)
/// resolves it on every machine by station NetworkIdentity, so the item lands in
/// the same place everywhere.
/// </summary>
public interface IStationPlacement
{
    Transform GetPlacementTransform();
}
