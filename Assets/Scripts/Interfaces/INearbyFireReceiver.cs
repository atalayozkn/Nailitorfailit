namespace Flammables
{
    // Tiles that never catch fire themselves but take damage from a burning neighbour.
    public interface INearbyFireReceiver
    {
        void OnNearbyFire();
    }
}
