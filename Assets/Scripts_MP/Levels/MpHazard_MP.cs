using System.Collections;
using System.Reflection;
using Interactions.Networking;
using Mirror;
using UnityEngine;

// Networked telegraphed hazard (faulty socket, burst pipe). SP runs the warning phase (glow, x-ray silhouette,
// "!" badge, repair window) only in singleplayer and fires instantly in multiplayer; and every machine used to roll
// its own hazards. Here the SERVER decides when a hazard starts (MpHazardScheduler_MP) and tells everyone; every
// machine then runs the SP warning/fire sequence at the same moment. Repairs go through the server, which checks the
// hazard is still repairable and tells everyone to stop it.
// Effects that SP routes through VfxManager/Sfx stay silent in multiplayer (those are gated in SP code).
public class Hazard_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly MethodInfo RunMethod = typeof(TelegraphedHazard).GetMethod("Run", NonPublic);
    private static readonly FieldInfo StateField = typeof(TelegraphedHazard).GetField("state", NonPublic);
    private static readonly FieldInfo RoutineField = typeof(TelegraphedHazard).GetField("routine", NonPublic);
    private static readonly PropertyInfo NameProperty = typeof(TelegraphedHazard).GetProperty("HazardName", NonPublic);

    private TelegraphedHazard hazard;

    private void Awake() => hazard = GetComponent<TelegraphedHazard>();

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureIndicatorHud();
    }

    // IndicatorHUD (the "!" badge) creates itself only in singleplayer; the component works as is.
    private static void EnsureIndicatorHud()
    {
        if (FindAnyObjectByType<IndicatorHUD>(FindObjectsInactive.Include) != null) return;

        new GameObject("IndicatorHUD (MP)").AddComponent<IndicatorHUD>();
    }

    private bool IsIdle => StateField == null || hazard == null || StateField.GetValue(hazard).ToString() == "Idle";

    private string HazardLabel => NameProperty != null && hazard != null ? (string)NameProperty.GetValue(hazard) : "Hazard";

    #region Start (server decides, everyone runs it)

    [Server]
    public void ServerTelegraph()
    {
        if (hazard == null || !hazard.isActiveAndEnabled || !IsIdle) return;

        InteractionDebug.Log($"[MP-HAZARD] '{name}' ({HazardLabel}) starts its warning.");
        RpcStartTelegraph();
    }

    [ClientRpc]
    private void RpcStartTelegraph()
    {
        if (hazard == null || !hazard.isActiveAndEnabled || !IsIdle || RunMethod == null) return;

        // Same as TelegraphedHazard.OnTrigger in singleplayer: start the warning coroutine and remember it, so a
        // repair can stop it.
        Coroutine routine = hazard.StartCoroutine((IEnumerator)RunMethod.Invoke(hazard, null));
        RoutineField?.SetValue(hazard, routine);

        MpNotice_MP.Warning($"{HazardLabel} is about to go off - fix it!", 4f);
    }

    #endregion

    #region Repair (through the server)

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        if (isServer) ServerRepair();
        else CmdRequestRepair();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestRepair(NetworkConnectionToClient sender = null) => ServerRepair();

    [Server]
    private void ServerRepair()
    {
        if (hazard == null || !hazard.IsRepairable) return;

        InteractionDebug.Log($"[MP-HAZARD] '{name}' ({HazardLabel}) repaired.");
        RpcRepair();
    }

    [ClientRpc]
    private void RpcRepair()
    {
        if (hazard != null) hazard.OnInteract();
    }

    #endregion
}
