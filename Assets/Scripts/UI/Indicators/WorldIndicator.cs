using System.Collections.Generic;
using UnityEngine;

// Marks a world object for IndicatorHUD: a round badge drawn over it on screen, pinned to the screen edge with an
// arrow when it is off screen, so the player always knows where it is. Hazards switch it on while they can be fixed;
// the shop stalls keep it on. Indicators sharing a groupKey show as one badge at the group's centre.
public class WorldIndicator : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Shown on the badge when no icon sprite is set.")]
    [SerializeField] private string glyph = "!";
    [Tooltip("Optional artist icon; replaces the glyph.")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Color color = new Color(1f, 0.55f, 0.1f);
    [SerializeField] private bool pulse = true;

    [Header("Placement")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
    [Tooltip("Indicators with the same key show as one badge at their centre (e.g. the shop stall row).")]
    [SerializeField] private string groupKey = "";

    [Header("Visibility")]
    [SerializeField] private bool shownOnStart;
    [Tooltip("Fades out when the player is closer than this (0 = never hides).")]
    [SerializeField] private float hideWithinDistance;

    private static readonly List<WorldIndicator> active = new();
    public static IReadOnlyList<WorldIndicator> Active => active;

    public string Glyph => glyph;
    public Sprite Icon => icon;
    public Color Color => color;
    public bool Pulse => pulse;
    public string GroupKey => groupKey;
    public float HideWithinDistance => hideWithinDistance;
    public Vector3 WorldPoint => transform.position + worldOffset;
    public bool IsShown { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList() => active.Clear();

    private void OnEnable()
    {
        active.Add(this);
        IsShown = shownOnStart;
        IndicatorHUD.EnsureExists();
    }
    private void OnDisable()
    {
        active.Remove(this);
        IsShown = false;
    }

    public void SetShown(bool shown)
    {
        IsShown = shown;
    }
}
