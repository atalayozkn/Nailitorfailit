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