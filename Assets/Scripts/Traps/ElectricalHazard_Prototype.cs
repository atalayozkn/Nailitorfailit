using Flammables;
using UnityEngine;
using UnityEngine.Events;
using Wettables;

// Faulty socket: crackles for telegraphDuration, then sparks once, electrifying wet tiles and lighting wood.
// Only repairable during the crackle; a missed one leaves the player to deal with the fire.
public class ElectricalHazard_Prototype : TelegraphedHazard
{
    [Header("Settings")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private Vector3 halfDimensions;

    [Header("Events")]
    [SerializeField] private UnityEvent onTriggerEvent;

    [Header("Debug")]
    [SerializeField] private bool isDebugMode;

    protected override string HazardName => "Socket";
    protected override VfxId WarningVfx => VfxId.SocketWarning;
    protected override Vector3 EffectPoint => transform.position;

    protected override void Fire()
    {
        onTriggerEvent?.Invoke();
        VfxManager.Play(VfxId.SocketSpark, muzzleTransform.position);

        Collider[] colliders = Physics.OverlapBox(muzzleTransform.position, halfDimensions, muzzleTransform.rotation, targetLayer);
        if (colliders.Length == 0) return;

        // Singleplayer: the spark lights only the nearest flammable tile; multiplayer keeps lighting all of them.
        bool singleSpark = !NetworkModeHelper.IsNetworked;
        IFlammable nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            if (col.gameObject.TryGetComponent<IWettable>(out var wettable))
            {
                wettable.OnElectrocute();
            }
            if (!col.gameObject.TryGetComponent<IFlammable>(out var flammable)) continue;
            if (!singleSpark)
            {
                flammable.OnFireStart();
                continue;
            }
            float distance = (col.bounds.center - muzzleTransform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = flammable;
            }
        }

        nearest?.OnFireStart();
    }

    private void OnDrawGizmos()
    {
        if (!isDebugMode || muzzleTransform == null) return;
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(muzzleTransform.position, muzzleTransform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfDimensions * 2f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
