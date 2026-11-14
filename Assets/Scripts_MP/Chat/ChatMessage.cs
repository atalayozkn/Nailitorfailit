using System;
using Mirror;

public struct ChatMessage : NetworkMessage
{
    public string sender;
    public string text;
    public int color; // MpPlayerColor_MP palette index of the sender, -1 = unknown
}

[Serializable]
public struct ChatEntry
{
    public string sender;
    public string text;
}
