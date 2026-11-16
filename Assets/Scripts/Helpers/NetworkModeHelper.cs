using Mirror;

// Lets shared gameplay code keep singleplayer-only systems out of network sessions.
public static class NetworkModeHelper
{
    public static bool IsNetworked => NetworkServer.active || NetworkClient.active;

    // Multiplayer switches this on (MpVfxNet_MP): particles and sounds that the shared code plays on whichever machine
    // runs it (hazards, fire, tile effects, stun stars...) stay on in a network session. They are purely local
    // visuals: every machine that runs the logic shows its own copy.
    public static bool NetworkPresentation { get; set; }

    // True when VfxManager / Sfx must stay silent: a network session that did not ask for the presentation layer.
    public static bool PresentationBlocked => IsNetworked && !NetworkPresentation;
}
