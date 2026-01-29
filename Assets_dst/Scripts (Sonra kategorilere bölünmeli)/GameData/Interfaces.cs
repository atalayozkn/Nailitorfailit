using Unity.Netcode;
using UnityEngine;

namespace Interactions
{
    /// <summary>
    /// Interface for any object that the player can pick up and carry.
    /// </summary>
    public interface IPickupable
    {
        // NetworkObject is needed for RPCs
        NetworkObject NetworkObject { get; }

        string ItemType { get; }
        float Weight { get; }

        Rigidbody GetRigidbody();

        void OnPickUp();
        void OnDrop();
    }


    /// Interface for any object that accepts items (e.g., Construct, TrashCan, Vehicle trunk).
    public interface IItemReceiver
    {
        NetworkObject NetworkObject { get; }

        // Returns true if the item was successfully accepted/consumed
        bool TryReceiveItem(IPickupable item);
    }
}