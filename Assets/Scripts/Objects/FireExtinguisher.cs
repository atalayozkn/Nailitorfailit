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
    private bool fuelInitialized;
    private void OnEnable()
    {
        // In a network session the item is switched on and off whenever it is picked up, dropped or re-attached:
        // that must not refill it (it could be used forever).
        if (!fuelInitialized || !NetworkModeHelper.IsNetworked)
        {
            currentFuel = maxFuel;
            hasUsed = false;
            fuelInitialized = true;
        }
        UpdateUI();
    }
    // The server's shared count (multiplayer): never gives fuel back.
    public void ApplyNetworkedFuel(int fuel)
    {
        fuelInitialized = true;
        if (fuel < currentFuel) currentFuel = Mathf.Max(0, fuel);
        if (fuel < 0) hasUsed = true;
    }
    public void OnUse()
    {
        if (hasUsed) return;

        // Multiplayer: the extinguisher lasts 5 uses (singleplayer keeps its own count).
        int step = NetworkModeHelper.IsNetworked ? maxFuel / NetworkedUses : perUseConsumption;
        currentFuel -= step;
        TryStopFire();
        EndSpray();
        // Networked: used up as soon as the last use is spent. Singleplayer: one use more (it goes below zero first).
        if (currentFuel < 0 || (NetworkModeHelper.IsNetworked && currentFuel <= 0))
        {
            currentFuel = 0;
            hasUsed = true;
            if (!ConsumeNetworked())
            {
                carriable.OnConsume();
                Invoke(nameof(Die), 0.1f);
            }
        }
        else if (NetworkModeHelper.IsNetworked && TryGetComponent(out CarriableObject_MP shared)) shared.ReportFuel(currentFuel);

        UpdateUI();
        onUseEvent?.Invoke();
    }
    // Multiplayer: the server removes the item for everyone (a local Destroy only emptied the user's own hand).
    private bool ConsumeNetworked()
    {
        if (!NetworkModeHelper.IsNetworked || !TryGetComponent(out CarriableObject_MP shared)) return false;

        PlayerInteractionHandler holder = GetComponentInParent<PlayerInteractionHandler>();
        shared.RequestConsume(holder != null ? holder.GetComponentInParent<Mirror.NetworkIdentity>() : null);
        return true;
    }
    private void Die()
    {
        Destroy(gameObject);
    }
    // Called by PlayerUseState when the use animation starts; OnUse ends it when the fires are put out.
    public void BeginSpray()
    {
        if (spray == null || hasUsed) return;

        // Multiplayer: the sound starts first, the powder jet follows a moment later.
        if (NetworkModeHelper.IsNetworked)
        {
            if (TryGetComponent(out CarriableObject_MP shared)) shared.ReportSpray(); // the other players show the jet too

            PlayerStateMachine holderStateMachine = GetComponentInParent<PlayerStateMachine>();
            if (holderStateMachine != null) holderStateMachine.onUseFE?.Invoke();
            CancelInvoke(nameof(StartSpray));
            Invoke(nameof(StartSpray), SoundLead);
            return;
        }

        StartSpray();
    }
    // Another player's use, seen on this machine: the jet plays for the length of the use animation.
    public void PlayRemoteSpray()
    {
        if (spray == null) return;

        CancelInvoke(nameof(StartSpray));
        CancelInvoke(nameof(EndSpray));
        Invoke(nameof(StartSpray), SoundLead);
        Invoke(nameof(EndSpray), RemoteSprayLength);
    }
    private const float RemoteSprayLength = 1.0f;
    private const float SoundLead = 0.15f;
    private const int NetworkedUses = 5;
    private void StartSpray()
    {
        if (spray == null || hasUsed) return;
        isSpraying = true;
        AimSpray();
        spray.Play(true);
    }
    public void EndSpray()
    {
        CancelInvoke(nameof(StartSpray));
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
