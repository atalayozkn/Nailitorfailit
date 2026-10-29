using UnityEngine;

public static class InteractionDebug
{

    public const string Version = "interact-v14 (modular MP: bootstrap + menu UI + setup tools)";

    public static bool Enabled = true;

    public static void Log(string message)
    {
        if (!Enabled) return;
        Debug.Log("[INTERACT] " + message);
    }

    public static void Log(string message, Object context)
    {
        if (!Enabled) return;
        Debug.Log("[INTERACT] " + message, context);
    }
}
