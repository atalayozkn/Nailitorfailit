using Mirror;
using UnityEngine;

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

    public void OnMailDelivered()
    {
        if (isServer) mailPresent = true;
        else CmdReportDelivery();
    }

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

        float reward = mailBox != null ? mailBox.GetTimeRewardAmount() : 0f;
        if (reward > 0f && GameTimeManager.Instance != null)
            GameTimeManager.Instance.IncreaseRoundTime(reward);
    }

    private void OnMailPresentChanged(bool oldValue, bool newValue)
    {

        if (mailBox != null) mailBox.ApplyMailPresentVisual(newValue);
    }
}
