using System;
using Mirror;

public struct ChatMessage : NetworkMessage
{
    public string sender;
    public string text;
}

[Serializable]
public struct ChatEntry
{
    public string sender;
    public string text;
}
