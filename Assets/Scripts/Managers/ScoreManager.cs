using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    //Instanced Does Destroy onLoad
    [SerializeField] private float baseLevelCompletionReward;
    [SerializeField] private float remainingTimeMultiplier;


    private ScoreHelper[] helpers;
    private float perObjectReward;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        //Find ScoreHelper's on the scene and cache them
        helpers = FindObjectsByType<ScoreHelper>(FindObjectsSortMode.None);

        perObjectReward = baseLevelCompletionReward / helpers.Length; //Calculation reward per object and cache it on awake
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int CalculateScore()
    {
        float score = 0f;

        foreach (ScoreHelper helper in helpers)
        {
            if (!GetActivity(helper)) continue;

            score += perObjectReward * GetPercentage(helper);
        }

        score += GameTimeManager.Instance.GetCurrentTime() * remainingTimeMultiplier;

        return Mathf.FloorToInt(score);
    }

    // Breaks the same calculation down room by room for the score screen. Defeat pays nothing.
    public ScoreReport BuildReport(bool isVictory)
    {
        ScoreReport report = new ScoreReport { isVictory = isVictory };

        List<RoomTally> rooms = GetRoomTallies();
        float running = 0f;
        float earned = 0f;
        float damageLoss = 0f;
        int damagedTiles = 0;
        int shown = 0;

        foreach (RoomTally room in rooms)
        {
            float full = perObjectReward * room.built;
            float roomEarned = perObjectReward * room.condition;
            float ratio = room.total > 0 ? room.condition / room.total : 0f;

            running += full;
            earned += roomEarned;
            damageLoss += full - roomEarned;
            damagedTiles += room.damaged;

            report.rows.Add(new ScoreRow
            {
                kind = ScoreRowKind.Room,
                label = room.name,
                detail = $"{room.built}/{room.total}",
                percent = $"{Mathf.RoundToInt(ratio * 100f)}%",
                grade = ScoreReport.GetGrade(ratio),
                isUnfinished = room.built < room.total,
                amount = TakeWholeAmount(running, ref shown)
            });
        }

        if (damagedTiles > 0)
        {
            running -= damageLoss;
            AddRow(report, ScoreRowKind.Cut, "Damaged tiles", $"{damagedTiles} tiles", running, ref shown);
        }

        if (isVictory)
        {
            float remaining = GameTimeManager.Instance.GetCurrentTime();
            running += remaining * remainingTimeMultiplier;
            AddRow(report, ScoreRowKind.Bonus, "Time left", $"{Mathf.FloorToInt(remaining / 60f):00}:{Mathf.FloorToInt(remaining % 60f):00}", running, ref shown);

            report.finalScore = CalculateScore();
            CorrectRounding(report, shown);
        }
        else
        {
            if (shown > 0) AddRow(report, ScoreRowKind.Cut, "Level not finished", "No pay", 0f, ref shown);
            report.finalScore = 0;
        }

        report.finalGrade = isVictory ? ScoreReport.GetGrade(helpers.Length > 0 ? earned / baseLevelCompletionReward : 0f) : "F";
        return report;
    }

    private List<RoomTally> GetRoomTallies()
    {
        Dictionary<RoomController, RoomTally> byRoom = new Dictionary<RoomController, RoomTally>();
        RoomTally noRoom = new RoomTally { name = "Other" };

        foreach (ScoreHelper helper in helpers)
        {
            RoomController controller = helper.GetComponentInParent<RoomController>(true);
            RoomTally room = noRoom;

            if (controller != null && !byRoom.TryGetValue(controller, out room))
            {
                room = new RoomTally { name = GetDisplayName(controller.name) };
                byRoom.Add(controller, room);
            }

            room.total++;
            if (!GetActivity(helper)) continue;

            float condition = GetPercentage(helper);
            room.built++;
            room.condition += condition;
            if (condition < 1f) room.damaged++;
        }

        List<RoomTally> rooms = new List<RoomTally>(byRoom.Values);
        rooms.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (noRoom.total > 0) rooms.Add(noRoom);
        return rooms;
    }

    private static void AddRow(ScoreReport report, ScoreRowKind kind, string label, string detail, float running, ref int shown)
    {
        int amount = TakeWholeAmount(running, ref shown);
        if (amount == 0) return;

        report.rows.Add(new ScoreRow { kind = kind, label = label, detail = detail, amount = amount });
    }

    // Rows show whole numbers, this way their rounding never adds up to a different total.
    private static int TakeWholeAmount(float running, ref int shown)
    {
        int whole = Mathf.FloorToInt(running + 0.001f);
        int amount = whole - shown;
        shown = whole;
        return amount;
    }

    private static void CorrectRounding(ScoreReport report, int shown)
    {
        int difference = report.finalScore - shown;
        if (difference != 0 && report.rows.Count > 0) report.rows[report.rows.Count - 1].amount += difference;
    }

    // LivingRoom -> Living Room
    private static string GetDisplayName(string objectName)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < objectName.Length; i++)
        {
            if (i > 0 && char.IsUpper(objectName[i]) && char.IsLower(objectName[i - 1])) builder.Append(' ');
            builder.Append(objectName[i]);
        }

        return builder.ToString();
    }

    private class RoomTally
    {
        public string name;
        public int total;
        public int built;
        public int damaged;
        public float condition;
    }

    //Utilities
    private bool GetActivity(ScoreHelper helper)
    {
        return helper.IsObjectConstructed();
    }

    private float GetPercentage(ScoreHelper helper)
    {
        return helper.GetObjectHealthPercent();
    }
}