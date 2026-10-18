using UnityEngine;

/// <summary>
/// Temporary, loud tracing for the multiplayer interaction chain:
/// detector -> proxy -> Command -> station logic -> replicated effect.
///
/// Turn it off by setting Enabled = false (or by removing the [INTERACT] logs) once
/// the flows are verified. Everything is prefixed so it is easy to grep in the
/// player log / editor console:  [INTERACT]
/// </summary>
public static class InteractionDebug
{
    /// <summary>
    /// Bumped on every interaction-system change so a running build can be identified
    /// from a single log line (stale builds were mistaken for broken code).
    /// </summary>
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
