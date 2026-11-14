using System.Collections.Generic;
using UnityEngine;

// Sits on the oil truck next to Car_Active. Spills from the moment it spawns until it despawns,
// so the whole lane it drives gets one oil strip. Strips are pooled outside the truck so they outlive it.
[RequireComponent(typeof(Car_Active))]
public class OilTruckSpiller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OilSlick slickPrefab;
    [Tooltip("Rear point the oil leaves from.")]
    [SerializeField] private Transform nozzle;

    [Header("Ground")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbe = 3f;

    private readonly List<OilSlick> pool = new();
    private readonly RaycastHit[] hits = new RaycastHit[8];
    private Transform poolRoot;

    private Vector3 startForward;
    private OilSlick current;
    private bool done;

    private void Awake()
    {
        // Pooled trucks are children of the car spawner, which never deactivates.
        poolRoot = transform.parent;
    }

    private void OnEnable()
    {
        current = null;
        done = false;
        startForward = transform.forward;
    }

    private void OnDisable()
    {
        StopSpill();
    }

    private void Update()
    {
        if (done || slickPrefab == null) return;

        Vector3 point = nozzle != null ? nozzle.position : transform.position;

        if (current == null)
        {
            if (!FindGround(point, out Vector3 ground)) return;

            current = GetSlick();
            current.Begin(ground, startForward);
            return;
        }

        current.Extend(point);
    }

    private void StopSpill()
    {
        if (current != null) current.Finish();
        current = null;
        done = true;
    }

    private bool FindGround(Vector3 point, out Vector3 ground)
    {
        ground = point;
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.5f, Vector3.down, hits, groundProbe, groundMask,
            QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i].collider;
            if (col.transform.IsChildOf(transform)) continue;
            if (col.CompareTag("Player")) continue;
            Rigidbody body = col.attachedRigidbody;
            if (body != null && !body.isKinematic) continue;
            if (hits[i].distance >= best) continue;

            best = hits[i].distance;
            ground = hits[i].point;
        }

        return best < float.MaxValue;
    }

    private OilSlick GetSlick()
    {
        foreach (OilSlick slick in pool)
        {
            if (slick != null && !slick.IsBusy) return slick;
        }

        OilSlick created = Instantiate(slickPrefab, poolRoot);
        created.gameObject.SetActive(false);
        pool.Add(created);
        return created;
    }
}
