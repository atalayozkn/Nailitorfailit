using Interactions.Networking;
using Mirror;
using UnityEngine;

public class PlayerMailBox_MP : NetworkBehaviour, IInteractionNetworkProxy
{
    [SerializeField] private MailBox mailBox;

    [SyncVar(hook = nameof(OnMailPresentChanged))]
    private bool mailPresent;

    public bool MailPresent => mailPresent;

    private void Awake()
    {
        if (mailBox == null) mailBox = GetComponentInChildren<MailBox>(true);
        AttachRelay();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (mailBox == null) mailBox = GetComponentInChildren<MailBox>(true);
        AttachRelay();
    }

    // This component sits on the player body, the MailBox somewhere else in the package: the interaction code looks for
    // a proxy from the MailBox upwards, so a tiny relay on the MailBox hands the interaction over to this component.
    private void AttachRelay()
    {
        if (mailBox == null) return;

        MpMailBoxRelay_MP relay = mailBox.GetComponent<MpMailBoxRelay_MP>();
        if (relay == null) relay = mailBox.gameObject.AddComponent<MpMailBoxRelay_MP>();
        relay.Target = this;
    }

    // E on the mailbox: whoever presses it, the server pays the time bonus once (nothing routed the interaction here
    // before, so the box never gave its bonus in multiplayer).
    public void RequestInteract(NetworkIdentity requester, Component interactable) => RequestClaim();

    // The mail man runs on the host only (one mail man for everybody) and tells his own MailBox when he arrives. The
    // server notices that here and makes it the truth for every machine.
    private static readonly System.Reflection.FieldInfo MailPresentField =
        typeof(MailBox).GetField("isMailPresent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    [ServerCallback]
    private void Update()
    {
        if (mailPresent || mailBox == null || MailPresentField == null) return;
        if ((bool)MailPresentField.GetValue(mailBox)) mailPresent = true;
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

        RpcMailCollected();
    }

    [ClientRpc]
    private void RpcMailCollected()
    {
        if (mailBox != null) VfxManager.Play(VfxId.MailCollect, mailBox.transform.position);
    }

    private void OnMailPresentChanged(bool oldValue, bool newValue)
    {

        if (mailBox != null) mailBox.ApplyMailPresentVisual(newValue);
    }
}

public class MpMailBoxRelay_MP : MonoBehaviour, IInteractionNetworkProxy
{
    public PlayerMailBox_MP Target;

    public void RequestInteract(NetworkIdentity requester, Component interactable)
    {
        if (Target != null) Target.RequestClaim();
    }
}
