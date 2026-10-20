using Mirror;
using UnityEngine;

/// <summary>
/// Server-authoritative match state: round timer + score.
///
/// Why: both were local per machine. The host granted round time (mail box, room
/// completion) and clients never saw it, each machine ticked its own clock (drift over
/// a match), and the score - which also depends on the remaining time - was computed
/// per machine. This component owns the values on the server and broadcasts them:
///
///   every 0.25 s : { remaining, serverTime, active }  -> clients interpolate by elapsed
///   every 0.5 s  : { score }                          -> clients display the server value
///
/// Message based on purpose (like the chat / wallet / level net): no scene or prefab
/// wiring, works from any level.
/// </summary>
public class MatchState_MP : MonoBehaviour
{
    public static MatchState_MP Instance { get; private set; }

    public struct TimeMessage : NetworkMessage
    {
        public float remaining;
        public double serverTime;
        public bool active;
    }

    public struct ScoreMessage : NetworkMessage
    {
        public int score;
    }

    public struct TimeRequestMessage : NetworkMessage
    {
        public float delta;
    }

    [SerializeField] private float timeBroadcastInterval = 0.25f;
    [SerializeField] private float scoreBroadcastInterval = 0.5f;

    private static bool hasScore;
    private static int serverScore;
    private float nextTimeAt;
    private float nextScoreAt;

    // last state, so a joining client can be brought up to date
    private float lastRemaining;
    private double lastServerTime;
    private bool lastActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MatchState_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MatchState_MP").AddComponent<MatchState_MP>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Called by LobbyNetworkManager on server/client start.</summary>
    public static void Register()
    {
        // Mirror clears handler tables on shutdown; re-register every session or the
        // second host/join in the same editor answers with "Unknown message id".
        if (NetworkServer.active)
        {
            NetworkServer.UnregisterHandler<TimeRequestMessage>();
            NetworkServer.RegisterHandler<TimeRequestMessage>(OnServerTimeRequest, false);
        }

        if (NetworkClient.active)
        {
            NetworkClient.UnregisterHandler<TimeMessage>();
            NetworkClient.RegisterHandler<TimeMessage>(OnTime, false);
            NetworkClient.UnregisterHandler<ScoreMessage>();
            NetworkClient.RegisterHandler<ScoreMessage>(OnScore, false);
        }
    }

    // ---------------------------------------------------------------- server

    private void Update()
    {
        if (!NetworkServer.active) return;

        if (Time.unscaledTime >= nextTimeAt)
        {
            nextTimeAt = Time.unscaledTime + Mathf.Max(0.05f, timeBroadcastInterval);
            BroadcastTime();
        }

        if (Time.unscaledTime >= nextScoreAt)
        {
            nextScoreAt = Time.unscaledTime + Mathf.Max(0.1f, scoreBroadcastInterval);
            BroadcastScore();
        }
    }

    private void BroadcastTime()
    {
        GameTimeManager time = GameTimeManager.Instance;

        lastRemaining = time != null ? time.GetCurrentTime() : 0f;
        lastServerTime = NetworkTime.time;
        lastActive = time != null && time.IsTimerActive();

        NetworkServer.SendToAll(new TimeMessage
        {
            remaining = lastRemaining,
            serverTime = lastServerTime,
            active = lastActive,
        }, Channels.Unreliable, true);
    }

    private void BroadcastScore()
    {
        ScoreManager score = ScoreManager.Instance;
        if (score == null) return;

        serverScore = score.CalculateScore();
        hasScore = true;

        NetworkServer.SendToAll(new ScoreMessage { score = serverScore }, Channels.Reliable, true);
    }

    /// <summary>Server: a client asks for extra round time (mail box, rewards).</summary>
    private static void OnServerTimeRequest(NetworkConnectionToClient conn, TimeRequestMessage message)
    {
        if (message.delta <= 0f) return;

        GameTimeManager.Instance?.IncreaseRoundTime(message.delta);
        Instance?.BroadcastTime();
    }

    /// <summary>The current state, for a machine that just connected.</summary>
    public static void SendStateTo(NetworkConnectionToClient conn)
    {
        if (!NetworkServer.active || conn == null) return;

        GameTimeManager time = GameTimeManager.Instance;

        conn.Send(new TimeMessage
        {
            remaining = time != null ? time.GetCurrentTime() : 0f,
            serverTime = NetworkTime.time,
            active = time != null && time.IsTimerActive(),
        });

        if (hasScore)
            conn.Send(new ScoreMessage { score = serverScore });
    }

    // ---------------------------------------------------------------- client

    private static void OnTime(TimeMessage message)
    {
        // The server value + the time it was sent: clients reconstruct the remaining
        // time from NetworkTime so they never drift away from the host's clock.
        GameTimeManager.Instance?.ApplyNetworkedTime(message.remaining, message.serverTime, message.active);
    }

    private static void OnScore(ScoreMessage message)
    {
        hasScore = true;
        serverScore = message.score;
        ScoreManager.Instance?.ApplyNetworkedScore(message.score);
    }

    /// <summary>MP client: ask the server to add round time.</summary>
    public void RequestTimeIncrease(float delta)
    {
        if (delta <= 0f) return;

        if (NetworkServer.active)
        {
            GameTimeManager.Instance?.IncreaseRoundTime(delta);
            BroadcastTime();
            return;
        }

        if (NetworkClient.isConnected) NetworkClient.Send(new TimeRequestMessage { delta = delta });
    }
}
