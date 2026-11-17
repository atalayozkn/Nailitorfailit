using Mirror;

// Network messages of the versus mode. They ride on the existing Mirror connection of the MP stack,
// so the versus scene needs no NetworkIdentity of its own.

// Server -> everyone, a few times per second.
public struct VersusStateMessage : NetworkMessage
{
    public uint playerA;
    public uint playerB;
    public float percentA;
    public float percentB;
    public int moneyA;
    public int moneyB;
    public int winner;
}

// Dog owner -> server: my dog rammed this player.
public struct VersusDogHitMessage : NetworkMessage
{
    public uint victim;
}

// Bat holder -> server, then server -> everyone (with the durability left).
public struct VersusBatSwingMessage : NetworkMessage
{
    public uint bat;
    public uint victim;
    public bool hit;
    public int remaining;
}

// Server -> everyone: the socket with this index sparks on both buildings.
public struct VersusFireMessage : NetworkMessage
{
    public int socketIndex;
}

// Any machine -> server -> everyone: this fire went out (extinguished or burnt out).
public struct VersusFireStopMessage : NetworkMessage
{
    public int fireIndex;
}

// Player -> server -> everyone: the warning on this socket was fixed.
public struct VersusRepairMessage : NetworkMessage
{
    public int side;
    public int socket;
}

// Player -> server -> everyone: this player switched to this inventory slot.
public struct VersusSlotMessage : NetworkMessage
{
    public uint player;
    public int slot;
}
