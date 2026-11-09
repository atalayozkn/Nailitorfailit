using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Plays the end of level score list row by row, then reveals the victory or defeat sign and the
// menu buttons beside it on the same screen. Sits on Panel_GameEnd. Click to skip the animation.
public class ScoreTallyUI : MonoBehaviour, IPointerClickHandler
{
    private enum TallyState
    {
        Idle,
        Playing,
        Finished
    }

    [Header("Layout")]
    [SerializeField] private Vector2 boardSize = new Vector2(860f, 820f);
    [SerializeField] private Vector2 boardPosition = new Vector2(-490f, 0f);
    [SerializeField] private float maxRowHeight = 76f;
    [SerializeField] private Vector2 endScreenPosition = new Vector2(500f, 0f);
    [SerializeField] private float endScreenScale = 0.62f;

    [Header("Timing")]
    [SerializeField] private float rowSlideTime = 0.22f;
    [SerializeField] private float minCountTime = 0.3f;
    [SerializeField] private float maxCountTime = 0.7f;
    [SerializeField] private float stampTime = 0.16f;
    [SerializeField] private float rowPause = 0.12f;
    [SerializeField] private float finalGradePause = 0.35f;
    [SerializeField] private float endScreenRevealTime = 0.35f;

    [Header("Colors")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color boardColor = new Color(0.87f, 0.64f, 0.4f, 1f);
    [SerializeField] private Color boardEdgeColor = new Color(0.15f, 0.09f, 0.05f, 1f);
    [SerializeField] private Color textColor = new Color(0.13f, 0.08f, 0.04f, 1f);
    [SerializeField] private Color gainColor = new Color(0.05f, 0.42f, 0.12f, 1f);
    [SerializeField] private Color cutColor = new Color(0.72f, 0.08f, 0.05f, 1f);

    private readonly List<GameObject> hiddenChildren = new List<GameObject>();
    private RectTransform board;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI finalGradeText;
    private TextMeshProUGUI hintText;
    private Image dim;
    private TallyState state = TallyState.Idle;
    private bool skipRequested;
    private int displayedScore;

    public void Play(ScoreReport report)
    {
        if (state != TallyState.Idle) return;

        CursorManager.Show(this); // the end screen has buttons; the cursor stays until the scene changes
        // Draw over the HUD, which sits after this panel on the canvas.
        transform.SetAsLastSibling();
        HideEndScreen();
        BuildBoard();
        StartCoroutine(PlayRoutine(report));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (state == TallyState.Playing) skipRequested = true;
    }

    private IEnumerator PlayRoutine(ScoreReport report)
    {
        state = TallyState.Playing;
        skipRequested = false;
        displayedScore = 0;

        float targetDim = dimColor.a;
        yield return Tween(0.25f, t =>
        {
            dim.color = new Color(dimColor.r, dimColor.g, dimColor.b, targetDim * t);
            board.localScale = Vector3.one * EaseOutBack(t);
        });

        // The list fills the space between the score at the top and the hint at the bottom.
        float listTop = boardSize.y * 0.5f - 240f;
        float rowHeight = Mathf.Min(maxRowHeight, (boardSize.y - 330f) / Mathf.Max(1, report.rows.Count));

        for (int i = 0; i < report.rows.Count; i++)
        {
            yield return PlayRow(report.rows[i], listTop - rowHeight * (i + 0.5f), rowHeight);
            yield return Wait(rowPause);
        }

        yield return Wait(finalGradePause);
        yield return PlayFinalGrade(report);

        SetScore(report.finalScore);
        hintText.text = string.Empty;

        yield return Wait(finalGradePause);
        yield return RevealEndScreen();
        state = TallyState.Finished;
    }

    // Panel_Victory or Panel_Defeat was picked by the level's end event, only that one comes back here.
    private IEnumerator RevealEndScreen()
    {
        List<RectTransform> revealed = new List<RectTransform>();

        foreach (GameObject child in hiddenChildren)
        {
            child.SetActive(true);

            RectTransform rect = (RectTransform)child.transform;
            rect.anchoredPosition = endScreenPosition;
            rect.localScale = Vector3.zero;
            revealed.Add(rect);
        }

        yield return Tween(endScreenRevealTime, t =>
        {
            foreach (RectTransform rect in revealed) rect.localScale = Vector3.one * (endScreenScale * EaseOutBack(t));
        });
    }

    private IEnumerator PlayRow(ScoreRow row, float y, float height)
    {
        RectTransform rowRect = CreateRect("Row", board, new Vector2(0f, y), new Vector2(boardSize.x - 80f, height));
        CanvasGroup group = rowRect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Color rowColor = row.kind == ScoreRowKind.Cut || row.isUnfinished ? cutColor : textColor;
        float fontSize = Mathf.Min(38f, height * 0.6f);

        CreateText("Label", rowRect, new Vector2(-235f, 0f), new Vector2(310f, height), row.label, fontSize, rowColor, TextAlignmentOptions.MidlineLeft);
        CreateText("Detail", rowRect, new Vector2(-5f, 0f), new Vector2(130f, height), row.detail, fontSize * 0.8f, rowColor, TextAlignmentOptions.Center);
        CreateText("Percent", rowRect, new Vector2(105f, 0f), new Vector2(90f, height), row.percent, fontSize * 0.8f, rowColor, TextAlignmentOptions.Center);
        TextMeshProUGUI gradeText = CreateText("Grade", rowRect, new Vector2(185f, 0f), new Vector2(70f, height), row.grade, fontSize * 1.15f, GetGradeColor(row.grade), TextAlignmentOptions.Center);
        gradeText.fontStyle = FontStyles.Bold;
        gradeText.alpha = 0f;

        Color amountColor = row.amount < 0 ? cutColor : gainColor;
        TextMeshProUGUI amountText = CreateText("Amount", rowRect, new Vector2(305f, 0f), new Vector2(150f, height), FormatAmount(0, row.amount), fontSize, amountColor, TextAlignmentOptions.MidlineRight);
        amountText.fontStyle = FontStyles.Bold;

        yield return Tween(rowSlideTime, t =>
        {
            group.alpha = t;
            rowRect.anchoredPosition = new Vector2(Mathf.Lerp(80f, 0f, EaseOutCubic(t)), y);
        });

        int startScore = displayedScore;
        float countTime = Mathf.Clamp(minCountTime + Mathf.Abs(row.amount) * 0.008f, minCountTime, maxCountTime);
        yield return Tween(countTime, t =>
        {
            int value = Mathf.RoundToInt(row.amount * EaseOutCubic(t));
            amountText.text = FormatAmount(value, row.amount);
            SetScore(startScore + value);
        });

        if (row.kind == ScoreRowKind.Cut) yield return Shake();
        else yield return Punch(scoreText.rectTransform, 1.15f);

        if (string.IsNullOrEmpty(row.grade)) yield break;

        gradeText.alpha = 1f;
        yield return Stamp(gradeText.rectTransform, 2.2f);
    }

    private IEnumerator PlayFinalGrade(ScoreReport report)
    {
        finalGradeText.text = report.finalGrade;
        finalGradeText.color = GetGradeColor(report.finalGrade);
        finalGradeText.alpha = 1f;

        yield return Tween(stampTime * 1.6f, t =>
        {
            float eased = EaseOutCubic(t);
            finalGradeText.rectTransform.localScale = Vector3.one * Mathf.Lerp(3.5f, 1f, eased);
            finalGradeText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-30f, -10f, eased));
        });

        yield return Shake();
    }

    private IEnumerator Stamp(RectTransform target, float fromScale)
    {
        yield return Tween(stampTime, t => target.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, EaseOutCubic(t)));
    }

    private IEnumerator Punch(RectTransform target, float peak)
    {
        yield return Tween(0.14f, t => target.localScale = Vector3.one * Mathf.Lerp(peak, 1f, EaseOutCubic(t)));
    }

    private IEnumerator Shake()
    {
        yield return Tween(0.18f, t =>
        {
            float strength = (1f - t) * 12f;
            board.anchoredPosition = boardPosition + (t < 1f ? new Vector2(Mathf.Sin(t * 40f) * strength, 0f) : Vector2.zero);
        });
    }

    // Skipping finishes every tween at once, so the whole list lands in its final state in a single frame.
    private IEnumerator Tween(float duration, Action<float> apply)
    {
        float elapsed = 0f;

        while (elapsed < duration && !skipRequested)
        {
            apply(elapsed / duration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        apply(1f);
    }

    private IEnumerator Wait(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration && !skipRequested)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private void HideEndScreen()
    {
        hiddenChildren.Clear();

        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;

            hiddenChildren.Add(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }

    private void BuildBoard()
    {
        RectTransform root = CreateRect("ScoreTally", transform, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        // Behind the end screen, otherwise the dim would block the Main Menu button.
        root.SetAsFirstSibling();

        // Stretches over the whole canvas whatever size the panel itself has, clicks land on this.
        RectTransform dimRect = CreateRect("Dim", root, Vector2.zero, new Vector2(6000f, 6000f));
        dim = dimRect.gameObject.AddComponent<Image>();
        dim.color = new Color(dimColor.r, dimColor.g, dimColor.b, 0f);

        board = CreateRect("Board", root, boardPosition, boardSize);
        board.localScale = Vector3.zero;
        Image boardImage = board.gameObject.AddComponent<Image>();
        boardImage.color = boardColor;
        boardImage.raycastTarget = false;
        Outline edge = board.gameObject.AddComponent<Outline>();
        edge.effectColor = boardEdgeColor;
        edge.effectDistance = new Vector2(8f, -8f);

        float top = boardSize.y * 0.5f;
        CreateText("ScoreTitle", board, new Vector2(0f, top - 60f), new Vector2(400f, 60f), "SCORE", 44f, textColor, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

        scoreText = CreateText("ScoreValue", board, new Vector2(0f, top - 145f), new Vector2(500f, 110f), "0", 100f, textColor, TextAlignmentOptions.Center);
        scoreText.fontStyle = FontStyles.Bold;

        finalGradeText = CreateText("FinalGrade", board, new Vector2(boardSize.x * 0.5f - 130f, top - 110f), new Vector2(200f, 200f), string.Empty, 170f, textColor, TextAlignmentOptions.Center);
        finalGradeText.fontStyle = FontStyles.Bold;
        finalGradeText.alpha = 0f;

        hintText = CreateText("Hint", board, new Vector2(0f, -top + 40f), new Vector2(boardSize.x - 80f, 50f), "Click to skip", 28f, textColor, TextAlignmentOptions.Center);
    }

    private void SetScore(int value)
    {
        displayedScore = value;
        scoreText.text = value.ToString();
    }

    private Color GetGradeColor(string grade)
    {
        switch (grade)
        {
            case "S":
            case "A":
                return gainColor;
            case "D":
            case "F":
                return cutColor;
            default:
                return textColor;
        }
    }

    private static string FormatAmount(int value, int finalAmount)
    {
        return finalAmount < 0 ? $"-{Mathf.Abs(value)}" : $"+{value}";
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject created = new GameObject(objectName, typeof(RectTransform));
        created.layer = parent.gameObject.layer;

        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string objectName, RectTransform parent, Vector2 position, Vector2 size, string content, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = CreateRect(objectName, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;
        float shifted = t - 1f;
        return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
    }
}
