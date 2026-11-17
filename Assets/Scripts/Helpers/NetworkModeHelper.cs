using Mirror;
using UnityEngine;

// Lets shared gameplay code keep singleplayer-only systems out of network sessions.
public static class NetworkModeHelper
{
    public static bool IsNetworked => NetworkServer.active || NetworkClient.active;

    // Multiplayer switches this on (MpVfxNet_MP): particles and sounds that the shared code plays on whichever machine
    // runs it (hazards, fire, tile effects, stun stars...) stay on in a network session. They are purely local
    // visuals: every machine that runs the logic shows its own copy.
    public static bool NetworkPresentation { get; set; }

    // True when VfxManager / Sfx must stay silent: a network session that asked for neither the co-op presentation
    // layer nor the full singleplayer feel (versus).
    public static bool PresentationBlocked => IsNetworked && !NetworkPresentation && !SingleplayerFeel;

    // A networked mode that still wants the singleplayer presentation and comfort features
    // (look-at panel, blueprint behaviour, VFX, SFX, warnings, indicators, inventory, hazard warnings).
    // The versus mode switches this on for its scene; co-op multiplayer leaves it off.
    public static bool SingleplayerFeel { get; set; }

    // True when those features must stay off: a network session that did not ask for them.
    public static bool PresentationOff => IsNetworked && !SingleplayerFeel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFlags() { SingleplayerFeel = false; NetworkPresentation = false; }

    // The player controlled on this machine. Null in a network session until that player has spawned.
    public static PlayerInteractionHandler LocalHandler()
    {
        if (!IsNetworked) return Object.FindFirstObjectByType<PlayerInteractionHandler>();
        if (NetworkClient.localPlayer == null) return null;
        return NetworkClient.localPlayer.GetComponentInChildren<PlayerInteractionHandler>(true);
    }

    // Whether this object is part of the local player's own package (their dog, bowl, mailbox...).
    public static bool IsLocalPlayers(Transform target)
    {
        if (!IsNetworked) return true;
        return NetworkClient.localPlayer != null && target.root == NetworkClient.localPlayer.transform.root;
    }
}
