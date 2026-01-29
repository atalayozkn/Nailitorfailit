using UnityEngine;

namespace Interactions
{
    public interface IPickupable
    {
        string ItemType { get; }
        float Weight { get; }
        MaterialType Material { get; }
        Tools Tool { get; }

        void OnPickUp();
        void OnDrop();
        Rigidbody GetRigidbody();
    }

}