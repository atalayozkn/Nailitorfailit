using System.Collections.Generic;

public enum ScoreRowKind
{
    Room,
    Cut,
    Bonus
}

// One line of the end of level score list.
public class ScoreRow
{
    public ScoreRowKind kind;
    public string label;
    public string detail;
    public string percent;
    public string grade;
    public bool isUnfinished;
    public int amount;
}

// What the score screen plays back. The amounts of all rows add up to finalScore.
public class ScoreReport
{
    public bool isVictory;
    public int finalScore;
    public string finalGrade;
    public readonly List<ScoreRow> rows = new List<ScoreRow>();

    public static string GetGrade(float ratio)
    {
        if (ratio >= 0.95f) return "S";
        if (ratio >= 0.85f) return "A";
        if (ratio >= 0.7f) return "B";
        if (ratio >= 0.5f) return "C";
        return "D";
    }
}
