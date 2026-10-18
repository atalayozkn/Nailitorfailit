using System.Collections.Generic;
using UnityEngine;

public enum ZoneKind
{

    PlayArea,

    DogPatrol,

    SystemZone,

    NoGo,
}

public enum BoundaryPolicy
{
    None,

    PushBack,

    Stun,

    Kill,

    Respawn,
}

[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public class ZoneVolume : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string zoneId = "";
    [SerializeField] private ZoneKind kind = ZoneKind.PlayArea;

    [Tooltip("The one PlayArea zone that represents the MAP BOUNDARY. Players outside " +
             "it are handled by the policy below. Mark exactly one PlayArea as primary.")]
    [SerializeField] private bool primaryPlayArea;

    [Header("Boundary policy (PlayArea)")]
    [SerializeField] private BoundaryPolicy policy = BoundaryPolicy.PushBack;
    [Tooltip("Inward nudge speed given to the player (m/s).")]
    [SerializeField] private float pushBackSpeed = 7f;
    [Tooltip("How far inside the edge the player is placed when pushed back.")]
    [SerializeField] private float pushBackMargin = 1.5f;
    [Tooltip("Seconds before the policy may fire again for the same player.")]
    [SerializeField] private float policyCooldown = 3f;

    [Header("Gizmos")]
    [SerializeField] private Color gizmoColor = new Color(0.15f, 0.8f, 0.45f, 0.12f);

    private BoxCollider box;

    public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId;
    public ZoneKind Kind => kind;
    public bool IsPrimaryPlayArea => primaryPlayArea;
    public BoundaryPolicy Policy => policy;
    public float PushBackSpeed => pushBackSpeed;
    public float PushBackMargin => pushBackMargin;
    public float PolicyCooldown => policyCooldown;
    public Vector3 Center => transform.TransformPoint(Box.center);

    public BoxCollider Box
    {
        get
        {
            if (box == null) box = GetComponent<BoxCollider>();
            return box;
        }
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider>();

        if (box != null && !box.isTrigger) box.isTrigger = true;
    }

    private void OnEnable() => ZoneRegistry.Register(this);
    private void OnDisable() => ZoneRegistry.Unregister(this);

    public bool Contains(Vector3 worldPoint)
    {
        BoxCollider b = Box;
        if (b == null) return false;

        Vector3 local = transform.InverseTransformPoint(worldPoint) - b.center;
        Vector3 half = b.size * 0.5f;

        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.y) <= half.y &&
               Mathf.Abs(local.z) <= half.z;
    }

    public Vector3 ClosestPointInside(Vector3 worldPoint, float margin)
    {
        BoxCollider b = Box;
        if (b == null) return worldPoint;

        Vector3 local = transform.InverseTransformPoint(worldPoint) - b.center;
        Vector3 half = b.size * 0.5f - Vector3.one * Mathf.Max(0.1f, margin);
        half = Vector3.Max(half, Vector3.one * 0.1f);

        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.y = Mathf.Clamp(local.y, -half.y, half.y);
        local.z = Mathf.Clamp(local.z, -half.z, half.z);

        return transform.TransformPoint(local + b.center);
    }

    public float SignedDistanceToEdge(Vector3 worldPoint)
    {
        BoxCollider b = Box;
        if (b == null) return 0f;

        Vector3 local = transform.InverseTransformPoint(worldPoint) - b.center;
        Vector3 half = b.size * 0.5f;

        Vector3 overshoot = new Vector3(
            Mathf.Max(0f, Mathf.Abs(local.x) - half.x),
            Mathf.Max(0f, Mathf.Abs(local.y) - half.y),
            Mathf.Max(0f, Mathf.Abs(local.z) - half.z));

        return overshoot.magnitude;
    }

    private void OnDrawGizmos()
    {
        BoxCollider b = Box;
        if (b == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
