using Mirror;
using UnityEngine;

// One-shot effects that the SP code plays inside logic MP runs on the server only (cutter product out,
// mixer done / fail, generator refuel). The server broadcasts them so every player sees and hears them.
public static class MpVfxNet_MP
{
    // Particles and sounds of the shared code were off in every network session. Hazard fire, water and sparks, tile
    // effects, stun stars and the like are local visuals, so multiplayer turns the layer on for all machines.
    // BeforeSceneLoad, not SubsystemRegistration: NetworkModeHelper clears its flags in SubsystemRegistration, and the
    // order of two methods in the same stage is not defined - when the clearing ran last, every particle and sound of
    // the shared code stayed off in the multiplayer session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnablePresentation() => NetworkModeHelper.NetworkPresentation = true;

    public struct VfxMessage : NetworkMessage
    {
        public int id;
        public Vector3 position;
    }

    // The scene's "aid" controller switches the mail man on with GameObject.SetActive on the level's own player package,
    // which does not exist in a multiplayer session. The server runs that schedule and tells every machine to switch
    // the mail man of the MP player package that keeps the level's mailbox.
    public struct MailManMessage : NetworkMessage { }

    public struct NoticeMessage : NetworkMessage
    {
        public string text;
    }

    // Called from LobbyNetworkManager.OnStartClient (handlers are cleared when the client shuts down).
    public static void RegisterClient()
    {
        NetworkClient.ReplaceHandler<VfxMessage>(OnVfx, false);
        NetworkClient.ReplaceHandler<MailManMessage>(_ => ActivateMailMan(), false);
        NetworkClient.ReplaceHandler<NoticeMessage>(message => MpNotice_MP.Warning(message.text, 4f), false);
    }

    // On-screen warning for one player (feedback that SP shows locally but MP decides on the server).
    public static void ServerNotify(NetworkIdentity player, string text)
    {
        if (player == null || player.connectionToClient == null)
        {
            MpNotice_MP.Warning(text, 4f);
            return;
        }

        player.connectionToClient.Send(new NoticeMessage { text = text });
    }

    public static void ServerPlay(VfxId id, Vector3 position)
    {
        if (!NetworkServer.active)
        {
            VfxManager.Play(id, position);
            return;
        }

        NetworkServer.SendToReady(new VfxMessage { id = (int)id, position = position }, Channels.Unreliable);
    }

    public static void ServerActivateMailMan()
    {
        if (!NetworkServer.active) return;
        NetworkServer.SendToReady(new MailManMessage());
    }

    private static void ActivateMailMan()
    {
        int switchedOn = 0;

        foreach (MailManController mailMan in Object.FindObjectsByType<MailManController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Only the mail man of an MP player package whose MailManPackage is on (the package that owns the mailbox).
            if (mailMan.transform.root.GetComponentInChildren<PlayerScripts.PlayerNetwork_MP>(true) == null) continue;
            if (mailMan.transform.parent != null && !mailMan.transform.parent.gameObject.activeInHierarchy) continue;
            if (mailMan.gameObject.activeSelf) continue;

            mailMan.gameObject.SetActive(true);
            switchedOn++;
        }

        InteractionDebug.Log($"[MP-MAIL] mail man switched on ({switchedOn}).");
    }

    private static void OnVfx(VfxMessage message) => VfxManager.Play((VfxId)message.id, message.position);
}
