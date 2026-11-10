using System.Collections;
using Interactions;
using ItemScript;
using UnityEngine;

// A hazard that warns before it goes off and can be repaired with one interact press.
// Idle: not interactable. Telegraph: warning effect, sound and pulsing glow, repairable.
// Active: the hazard runs (Fire), repairable only when repairableWhileActive. Repairing cancels this event only;
// the hazard controller can trigger it again later. Triggers that arrive while it is busy are ignored.
// Networked sessions keep the old instant behaviour (see NetworkModeHelper).
public abstract class TelegraphedHazard : MonoBehaviour, IInteractable, ILookAtInfo
{
    private enum HazardState { Idle, Telegraph, Active }

    [Header("Telegraph")]
    [SerializeField] protected float telegraphDuration = 2.5f;
    [Tooltip("How long the hazard runs once it goes off. 0 for a one-shot.")]
    [SerializeField] protected float activeDuration;
    [Tooltip("Can still be repaired while the hazard runs, not only during the warning.")]
    [SerializeField] protected bool repairableWhileActive;
    [SerializeField] private float warningInterval = 0.6f;
    [SerializeField] private float glowPulseInterval = 0.3f;

    [Header("Repair")]
    [Tooltip("Trigger on the Interaction layer, only enabled while the hazard can be repaired.")]
    [SerializeField] private Collider repairCollider;
    [SerializeField] private HighlightHelper highlight;
    [Tooltip("See-through silhouette; pulses its warning colour while the hazard can be repaired, so it reads under the roof.")]
    [SerializeField] private XRayOverlay xray;
    [Tooltip("'!' badge that follows the hazard on screen, or points to it from the screen edge, while it can be repaired.")]
    [SerializeField] private WorldIndicator indicator;

    public InteractableType InteractableType => InteractableType.Station;

    // Raised when a hazard starts its warning (singleplayer). A loyal dog listens and leads the player to it.
    public static event System.Action<TelegraphedHazard> WarningStarted;
    // True while the hazard can still be fixed (warning, and the leak for pipes).
    public bool IsRepairable => CanRepair;

    private HazardState state = HazardState.Idle;
    private Coroutine routine;
    private bool isHovered;

    protected abstract string HazardName { get; }
    protected abstract VfxId WarningVfx { get; }
    protected abstract Vector3 EffectPoint { get; }
    // The hazard itself: wets tiles, starts fires.
    protected abstract void Fire();
    // Stops a running hazard early after a repair (e.g. the water spray).
    protected virtual void StopActive() { }

    protected virtual void Awake()
    {
        SetRepairable(false);
    }
    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        state = HazardState.Idle;
        SetRepairable(false);
    }

    // Called by the hazard controller.
    public void OnTrigger()
    {
        if (NetworkModeHelper.IsNetworked)
        {
            Fire();
            return;
        }
        if (state != HazardState.Idle) return;
        routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        state = HazardState.Telegraph;
        SetRepairable(true);
        WarningStarted?.Invoke(this);

        float elapsed = 0f, nextWarning = 0f, nextPulse = 0f;
        bool glowOn = false;
        while (elapsed < telegraphDuration)
        {
            if (elapsed >= nextWarning)
            {
                VfxManager.Play(WarningVfx, EffectPoint);
                nextWarning += warningInterval;
            }
            if (elapsed >= nextPulse)
            {
                glowOn = !glowOn;
                SetGlow(isHovered || glowOn);
                nextPulse += glowPulseInterval;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        state = HazardState.Active;
        if (!repairableWhileActive) SetRepairable(false);
        else SetGlow(true);
        Fire();

        if (activeDuration > 0f) yield return new WaitForSeconds(activeDuration);

        state = HazardState.Idle;
        SetRepairable(false);
        routine = null;
    }

    private bool CanRepair => state == HazardState.Telegraph || (state == HazardState.Active && repairableWhileActive);

    public bool OnInteract()
    {
        if (!CanRepair) return false;

        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (state == HazardState.Active) StopActive();
        state = HazardState.Idle;
        SetRepairable(false);
        VfxManager.Play(VfxId.HazardRepaired, EffectPoint);
        return true;
    }
    public void OnHoverOn()
    {
        isHovered = true;
        if (CanRepair) SetGlow(true);
    }
    public void OnHoverOff()
    {
        isHovered = false;
    }
    public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
    {
        if (!CanRepair) return default;
        return LookAtInfo.Ready(HazardName, "Fix " + HazardName.ToLowerInvariant());
    }

    private void SetRepairable(bool repairable)
    {
        if (repairCollider != null) repairCollider.enabled = repairable;
        if (xray != null) xray.SetWarning(repairable);
        if (indicator != null) indicator.SetShown(repairable);
        if (!repairable)
        {
            isHovered = false;
            SetGlow(false);
        }
    }
    private void SetGlow(bool on)
    {
        if (highlight == null) return;
        if (on) highlight.HighlightMaterial();
        else highlight.ReverseHighlight();
    }
}
