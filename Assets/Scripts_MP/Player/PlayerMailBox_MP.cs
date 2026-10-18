using Mirror;
using UnityEngine;

/// <summary>
/// Networked mail state for THIS player's mailbox.
///
/// The mail package (mailman + mailbox) lives inside the player prefab, so every
/// player has their own mailbox and the "a letter is waiting" state is per-player.
/// Locally it lived only in the MailBox component of whichever machine noticed the
/// delivery: the letter appeared on one machine and the time reward was granted
/// locally, so the round timer and the visuals disagreed between machines.
///
/// Here the state is a SyncVar, deliveries and claims are Commands, and the round
/// time reward is granted ON THE SERVER - the MailBox pulls in its visual events
/// from the SyncVar hook, so every machine shows the same mail.
/// </summary>
public class PlayerMailBox_MP : NetworkBehaviour
{
    [SerializeField] private MailBox mailBox;

    [SyncVar(hook = nameof(OnMailPresentChanged))]
    private bool mailPresent;

    public bool MailPresent => mailPresent;

    private void Awake()
    {
        if (mailBox == null) mailBox = GetComponentInChildren<MailBox>(true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (mailBox == null) mailBox = GetComponentInChildren<MailBox>(true);
    }

    /// <summary>Called by MailBox.OnTrigger() - the mailman delivered a letter.</summary>
    public void OnMailDelivered()
    {
        if (isServer) mailPresent = true;
        else CmdReportDelivery();
    }

    /// <summary>Called by MailBox.OnInteract() - the player claims the letter.</summary>
    public void RequestClaim()
    {
        if (isServer) ServerClaim();
        else CmdRequestClaim();
    }

    [Command(requiresAuthority = false)]
    private void CmdReportDelivery() => mailPresent = true;

    [Command(requiresAuthority = false)]
    private void CmdRequestClaim() => ServerClaim();

    [Server]
    private void ServerClaim()
    {
        if (!mailPresent) return;

        mailPresent = false;

        // The round timer is the server's business.
        float reward = mailBox != null ? mailBox.GetTimeRewardAmount() : 0f;
        if (reward > 0f && GameTimeManager.Instance != null)
            GameTimeManager.Instance.IncreaseRoundTime(reward);
    }

    private void OnMailPresentChanged(bool oldValue, bool newValue)
    {
        // Visuals on every machine; the MailBox fires its own UnityEvents.
        if (mailBox != null) mailBox.ApplyMailPresentVisual(newValue);
    }
}
