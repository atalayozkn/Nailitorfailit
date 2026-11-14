using Mirror;
using UnityEngine;

// One-shot effects that the SP code plays inside logic MP runs on the server only (cutter product out,
// mixer done / fail, generator refuel). The server broadcasts them so every player sees and hears them.
public static class MpVfxNet_MP
{
    public struct VfxMessage : NetworkMessage
    {
        public int id;
        public Vector3 position;
    }

    public struct NoticeMessage : NetworkMessage
    {
        public string text;
    }

    // Called from LobbyNetworkManager.OnStartClient (handlers are cleared when the client shuts down).
    public static void RegisterClient()
    {
        NetworkClient.ReplaceHandler<VfxMessage>(OnVfx, false);
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

    private static void OnVfx(VfxMessage message) => VfxManager.Play((VfxId)message.id, message.position);
}
