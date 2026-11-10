using Flammables;
using Interactions;
using ItemScript;
using UnityEngine;
using UnityEngine.Events;
public class FireExtinguisher : MonoBehaviour, IUsable
{
    [Header("References")]
    [SerializeField] CarriableObject_SP carriable;
    [Header("Fuel Settings")]
    [SerializeField] private int maxFuel = 100;
    [SerializeField] private int perUseConsumption = 10;
    [Header("Detection Settings")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Vector3 halfDimensions;
    [SerializeField] private Transform muzzle;
    [Header("Spray")]
    [Tooltip("Powder jet. Plays for the whole use animation, aimed along the holder's facing.")]
    [SerializeField] private ParticleSystem spray;
    [SerializeField] private float sprayDownAngle = 12f;
    [Header("Debug Settings")]
    [SerializeField] private bool debugMode;
    public UseType UseType => UseType.FireExtinguisher;
    [SerializeField] private UnityEvent onUseEvent;

    private int currentFuel;
    private bool hasUsed;
    private bool isSpraying;
    private void OnEnable()
    {
        currentFuel = maxFuel;
        hasUsed = false;
        UpdateUI();
    }
    public void OnUse()
    {
        if (hasUsed) return;

        currentFuel -= perUseConsumption;
        TryStopFire();
        EndSpray();
        if (currentFuel < 0)
        {
            currentFuel = 0;
            hasUsed = true;
            carriable.OnConsume();
            Invoke(nameof(Die), 0.1f);
        }

        UpdateUI();
        onUseEvent?.Invoke();
    }
    private void Die()
    {
        Destroy(gameObject);
    }
    // Called by PlayerUseState when the use animation starts; OnUse ends it when the fires are put out.
    public void BeginSpray()
    {
        if (spray == null || hasUsed) return;
        isSpraying = true;
        AimSpray();
        spray.Play(true);
    }
    public void EndSpray()
    {
        isSpraying = false;
        if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
    private void LateUpdate()
    {
        if (isSpraying) AimSpray();
    }
    private void AimSpray()
    {
        GetSprayBox(out _, out Quaternion facing);
        spray.transform.SetPositionAndRotation(muzzle.position, facing * Quaternion.Euler(sprayDownAngle, 0f, 0f));
    }
    private void TryStopFire()
    {
        GetSprayBox(out Vector3 center, out Quaternion rotation);
        Collider[] cols = Physics.OverlapBox(center, halfDimensions, rotation, targetLayer);
        if (cols.Length == 0) return;

        var stopped = new System.Collections.Generic.HashSet<IFlammable>();
        foreach (var col in cols)
        {
            // The fire script can sit on a parent of the collider that was hit.
            var flammable = col.GetComponentInParent<IFlammable>();
            if (flammable != null && stopped.Add(flammable)) flammable.OnFireStop();
        }
    }

    // The extinguisher is held in the right hand, so the muzzle follows the hand bone and its animation.
    // The spray box is built from the holder's feet along their flat facing instead: it starts at the
    // player and reaches halfDimensions.z * 2 ahead, covering floor tiles and walls in front.
    // Without a player holder (e.g. the multiplayer copy) it keeps the old muzzle-centred box.
    private void GetSprayBox(out Vector3 center, out Quaternion rotation)
    {
        var holder = GetComponentInParent<PlayerInteractionHandler>();
        if (holder == null)
        {
            center = muzzle.position;
            rotation = muzzle.rotation;
            return;
        }
        Vector3 forward = Vector3.ProjectOnPlane(holder.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        center = holder.transform.position + rotation * new Vector3(0f, halfDimensions.y, halfDimensions.z);
    }
    private void UpdateUI()
    {

    }
    private void OnDrawGizmosSelected()
    {
        if (!debugMode) return;
        GetSprayBox(out Vector3 center, out Quaternion rotation);
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfDimensions * 2f);
    }
}
