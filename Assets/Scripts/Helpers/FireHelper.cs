using System.Collections;
using System.Collections.Generic;
using Flammables;
using UnityEngine;
using UnityEngine.Events;

public class FireHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObjectHealth objectHealth;

    [Header("Settings")]
    [SerializeField] private float fireHitInterval = 1f;

    [Header("Singleplayer Fire")]
    [SerializeField] private float singleplayerHitInterval = 3f;
    [SerializeField] private float spreadInterval = 10f;
    [Tooltip("Reaches the 8 surrounding tiles on the 1m grid (glass and brick take heat from all of them).")]
    [SerializeField] private float neighbourRadius = 1.2f;
    [Tooltip("Fire only jumps to wood whose centre is this close: the 4 edge neighbours on the 1m grid, no diagonals.")]
    [SerializeField] private float spreadDistance = 1.1f;
    [Tooltip("Most wood tiles burning at once in a level. New fires (spread or socket) are refused above this.")]
    [SerializeField] private int maxConcurrentFires = 3;

    // Singleplayer count of burning tiles, for the cap. Cleared on play start for domain-reload-free play mode.
    private static int burningCount;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() => burningCount = 0;
    public static bool AtFireCap(int cap) => !NetworkModeHelper.IsNetworked && burningCount >= cap;

    [Header("Events")]
    [SerializeField] private UnityEvent onFireStarted;
    [SerializeField] private UnityEvent onFireStopped;

    private readonly Collider[] neighbours = new Collider[16];
    private readonly List<WoodTile> spreadTargets = new();
    private Coroutine fireRoutine;
    private bool isBurning;
    private GameObject burnEffect;
    private int tileMask;

    private void Awake()
    {
        tileMask = LayerMask.GetMask("Ground");
    }
    private void OnEnable()
    {
        isBurning = false;
        VfxManager.Stop(burnEffect);
        if (fireRoutine != null) StopCoroutine(fireRoutine);
        fireRoutine = null;
    }
    private void OnDisable()
    {
        StopFire();
    }
    public void StartFire()
    {
        if (isBurning) return;
        if (AtFireCap(maxConcurrentFires)) return;
        isBurning = true;
        burningCount++;
        onFireStarted?.Invoke();
        VfxManager.Play(VfxId.FireIgnite, transform.position);
        burnEffect = VfxManager.PlayLoop(VfxId.FireBurn, transform);
        fireRoutine = StartCoroutine(FireRoutine());
    }
    public void StopFire()
    {
        if (!isBurning) return;

        isBurning = false;
        burningCount = Mathf.Max(0, burningCount - 1);
        VfxManager.Stop(burnEffect);

        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
            fireRoutine = null;
        }

        onFireStopped?.Invoke();
    }
    private IEnumerator FireRoutine()
    {
        bool isNetworked = NetworkModeHelper.IsNetworked;
        float hitInterval = isNetworked ? fireHitInterval : singleplayerHitInterval;
        float spreadTimer = 0f;

        while (true)
        {
            objectHealth.DealDamage();

            if (!isNetworked)
            {
                spreadTimer += hitInterval;
                bool shouldSpread = spreadTimer >= spreadInterval;
                if (shouldSpread) spreadTimer = 0f;
                AffectNeighbours(shouldSpread);
            }

            yield return new WaitForSeconds(hitInterval);
        }
    }
    // Burning wood damages the glass and brick around it, and now and then ignites one wood neighbour.
    private void AffectNeighbours(bool shouldSpread)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, neighbourRadius, neighbours, tileMask, QueryTriggerInteraction.Ignore);
        spreadTargets.Clear();
        Vector3 ownCenter = TryGetComponent<Collider>(out var own) ? own.bounds.center : transform.position;

        for (int i = 0; i < count; i++)
        {
            GameObject neighbour = neighbours[i].gameObject;
            if (neighbour == gameObject) continue;

            if (neighbour.TryGetComponent<INearbyFireReceiver>(out var receiver)) receiver.OnNearbyFire();

            if (!neighbour.TryGetComponent<WoodTile>(out var wood) || wood.IsBurning()) continue;
            Vector3 offset = neighbours[i].bounds.center - ownCenter;
            if (offset.sqrMagnitude <= spreadDistance * spreadDistance) spreadTargets.Add(wood);
        }

        if (shouldSpread && spreadTargets.Count > 0 && !AtFireCap(maxConcurrentFires)) spreadTargets[Random.Range(0, spreadTargets.Count)].OnFireStart();
    }
    public bool IsBurning()
    {
        return isBurning;
    }
}
