using Mirror;
using UnityEngine;

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

    public static void Register()
    {

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

    private static void OnServerTimeRequest(NetworkConnectionToClient conn, TimeRequestMessage message)
    {
        if (message.delta <= 0f) return;

        GameTimeManager.Instance?.IncreaseRoundTime(message.delta);
        Instance?.BroadcastTime();
    }

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

    private static void OnTime(TimeMessage message)
    {

        GameTimeManager.Instance?.ApplyNetworkedTime(message.remaining, message.serverTime, message.active);
    }

    private static void OnScore(ScoreMessage message)
    {
        hasScore = true;
        serverScore = message.score;
        ScoreManager.Instance?.ApplyNetworkedScore(message.score);
    }

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
