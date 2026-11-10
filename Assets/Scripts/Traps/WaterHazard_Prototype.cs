using UnityEngine;
using UnityEngine.Events;
using Wettables;

// Burst pipe: drips for telegraphDuration, then sprays water (the spray particles wet the tiles they hit).
// Can be repaired from the first drip until the spray ends.
public class WaterHazard_Prototype : TelegraphedHazard
{
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private Vector3 halfDimensions;
    [Tooltip("The water spray, stopped early when the pipe is repaired mid-leak.")]
    [SerializeField] private ParticleSystem spray;

    [Header("Events")]
    [SerializeField] private UnityEvent onTriggerEvent;

    [Header("Debug")]
    [SerializeField] private bool isDebugMode;

    protected override string HazardName => "Pipe";
    protected override VfxId WarningVfx => VfxId.PipeWarning;
    protected override Vector3 EffectPoint => muzzleTransform.position;

    protected override void Fire()
    {
        onTriggerEvent?.Invoke();
        VfxManager.Play(VfxId.PipeBurst, muzzleTransform.position);

        Collider[] colliders = Physics.OverlapBox(muzzleTransform.position, halfDimensions, muzzleTransform.rotation, targetLayer);
        if (colliders.Length == 0) return;

        foreach (var col in colliders)
        {
            if (col.gameObject.TryGetComponent<IWettable>(out var wettable))
            {
                wettable.OnWaterContact();
            }
        }
    }
    protected override void StopActive()
    {
        if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
