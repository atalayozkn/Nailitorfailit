using TMPro;
using UnityEngine;

public class RoomController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomText;
    [SerializeField] private GameObject[] availableTiles;
    [SerializeField] private GameObject wallStructure;
    [Header("Tutorial Settings")]
    [SerializeField] private bool isTutorial = false;
    [SerializeField] private TutorialObjective objective;
    private int completedItemCount;
    private int totalRequiredItemCount;

    private bool isCompleted = false;
    private void Awake()
    {
        totalRequiredItemCount = availableTiles.Length;
        UpdateUI();
        wallStructure.SetActive(false);
    }
    public void IncreaseCounter()
    {
        completedItemCount++;
        CheckForCompletion();
        UpdateUI();
    }
    public void ReduceCounter()
    {
        completedItemCount--;
        UpdateUI();
    }
    private void CheckForCompletion()
    {
        if (completedItemCount >= totalRequiredItemCount && !isCompleted)
        {
            isCompleted = true;
            wallStructure.SetActive(true);
            NotifyLevelManager();
        }
    }
    private void UpdateUI()
    {
        int diff = totalRequiredItemCount - completedItemCount;
        if (roomText == null) return;
        roomText.text = diff.ToString();
    }
    private void NotifyLevelManager()
    {
        if (isTutorial) objective.CompleteCondition();
        else
        {
            LevelManager.Instance.CompleteRoom();
        }
    }
    public bool IsCompleted()
    {
        return isCompleted;
    }
    public int GetMissingTileCount()
    {
        return totalRequiredItemCount - completedItemCount;
    }
}
