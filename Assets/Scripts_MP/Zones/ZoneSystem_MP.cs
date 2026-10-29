using System;
using System.Collections.Generic;
using PlayerScripts;
using Mirror;
using UnityEngine;

public class ZoneSystem_MP : MonoBehaviour
{
    public static ZoneSystem_MP Instance { get; private set; }

    [Header("Enforcement")]
    [Tooltip("How often zone/boundary checks run (seconds).")]
    [SerializeField] private float evaluateInterval = 0.15f;
    [Tooltip("Server enforces Kill/Respawn; the owner always enforces PushBack/Stun.")]
    [SerializeField] private bool serverEnforcesKill = true;
    [Tooltip("Owner machine pushes its own player back instantly (recommended).")]
    [SerializeField] private bool ownerPushesBack = true;
    [Tooltip("Log every boundary event (debug).")]
    [SerializeField] private bool logEvents;

    public event Action<PlayerNetwork_MP, ZoneVolume> PlayerEnteredZone;
    public event Action<PlayerNetwork_MP, ZoneVolume> PlayerExitedZone;

    private readonly Dictionary<PlayerNetwork_MP, HashSet<ZoneVolume>> membership =
        new Dictionary<PlayerNetwork_MP, HashSet<ZoneVolume>>();

    private readonly Dictionary<PlayerNetwork_MP, float> lastPolicyTime =
        new Dictionary<PlayerNetwork_MP, float>();

    private float nextEvaluateAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<ZoneSystem_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("ZoneSystem_MP").AddComponent<ZoneSystem_MP>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextEvaluateAt) return;
        nextEvaluateAt = Time.unscaledTime + Mathf.Max(0.02f, evaluateInterval);

        if (NetworkServer.active)
        {
            EvaluateServerSide();
            return;
        }

        EvaluateOwnerSide();
    }

    private void EvaluateServerSide()
    {
        foreach (PlayerNetwork_MP player in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            if (player == null) continue;

            UpdateMembership(player, raiseEvents: true);

            ZoneVolume boundary = ZoneRegistry.PrimaryPlayArea();
            if (boundary == null) continue;
            if (boundary.Contains(player.transform.position)) continue;

            if (logEvents)
                Debug.Log($"[ZONE] {player.name} left play area '{boundary.ZoneId}' " +
                          $"(distance {boundary.SignedDistanceToEdge(player.transform.position):F1} m)");

            if (serverEnforcesKill)
                ApplyPolicy(player, boundary, ownerMachineHandlesPushBack: true);
        }
    }

    private void EvaluateOwnerSide()
    {
        foreach (PlayerNetwork_MP player in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            if (player == null || !player.isOwned) continue;

            UpdateMembership(player, raiseEvents: false);

            ZoneVolume boundary = ZoneRegistry.PrimaryPlayArea();
            if (boundary == null) continue;
            if (boundary.Contains(player.transform.position)) continue;

            ApplyPolicy(player, boundary, ownerMachineHandlesPushBack: true);
        }
    }

    private void ApplyPolicy(PlayerNetwork_MP player, ZoneVolume zone, bool ownerMachineHandlesPushBack)
    {
        if (!CooldownElapsed(player, zone)) return;

        switch (zone.Policy)
        {
            case BoundaryPolicy.None:
                break;

            case BoundaryPolicy.PushBack:
                if (ownerPushesBack && (player.isOwned || player.isServer))
                    PushBack(player, zone);
                break;

            case BoundaryPolicy.Stun:
                if (player.isOwned)
                {
                    PlayerStateMachine stateMachine = player.GetComponent<PlayerStateMachine>();
                    if (stateMachine != null && !stateMachine.isDead)
                        stateMachine.ChangeToStunnedState();
                }
                break;

            case BoundaryPolicy.Kill:
            case BoundaryPolicy.Respawn:

                if (player.isServer || player.isOwned)
                    player.RequestDeath();
                break;
        }
    }

    private bool CooldownElapsed(PlayerNetwork_MP player, ZoneVolume zone)
    {
        float cooldown = Mathf.Max(0.1f, zone.PolicyCooldown);

        if (lastPolicyTime.TryGetValue(player, out float last) && Time.unscaledTime - last < cooldown)
            return false;

        lastPolicyTime[player] = Time.unscaledTime;
        return true;
    }

    private static void PushBack(PlayerNetwork_MP player, ZoneVolume zone)
    {
        Vector3 target = zone.ClosestPointInside(player.transform.position, zone.PushBackMargin);

        player.transform.position = target;

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body == null) return;

        body.position = target;

        Vector3 inward = zone.Center - target;
        inward.y = 0f;

        if (inward.sqrMagnitude > 0.0001f)
        {
            inward.Normalize();
            body.linearVelocity = inward * zone.PushBackSpeed;
        }
        else
        {
            body.linearVelocity = Vector3.zero;
        }

        body.angularVelocity = Vector3.zero;
    }

    private void UpdateMembership(PlayerNetwork_MP player, bool raiseEvents)
    {
        List<ZoneVolume> current = ZoneRegistry.Containing(player.transform.position);

        if (!membership.TryGetValue(player, out HashSet<ZoneVolume> previous))
        {
            previous = new HashSet<ZoneVolume>();
            membership[player] = previous;
        }

        foreach (ZoneVolume zone in current)
        {
            if (previous.Contains(zone)) continue;

            previous.Add(zone);

            if (!raiseEvents) continue;
            if (logEvents) Debug.Log($"[ZONE] {player.name} entered '{zone.ZoneId}' ({zone.Kind})");
            PlayerEnteredZone?.Invoke(player, zone);
        }

        var exited = new List<ZoneVolume>();
        foreach (ZoneVolume zone in previous)
        {
            if (current.Contains(zone)) continue;
            exited.Add(zone);
        }

        foreach (ZoneVolume zone in exited)
        {
            previous.Remove(zone);

            if (!raiseEvents) continue;
            if (logEvents) Debug.Log($"[ZONE] {player.name} exited '{zone.ZoneId}' ({zone.Kind})");
            PlayerExitedZone?.Invoke(player, zone);
        }
    }

    public static ZoneVolume GetZone(string id, ZoneKind? kind = null) => ZoneRegistry.Find(id, kind);

    public static bool IsInside(string id, Vector3 position) => ZoneRegistry.IsInside(id, position);

    public static List<ZoneVolume> ZonesAt(Vector3 position, ZoneKind? kind = null) =>
        ZoneRegistry.Containing(position, kind);
}
