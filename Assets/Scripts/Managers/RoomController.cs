using TMPro;
using UnityEngine;

public class RoomController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomText;
    [SerializeField] private GameObject[] availableTiles;

    private int completedItemCount;
    private int totalRequiredItemCount;

    private bool isCompleted = false;
    private void Awake()
    {
        totalRequiredItemCount = availableTiles.Length;
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
            NotifyLevelManager();
        }
    }
    private void UpdateUI()
    {
        int diff = totalRequiredItemCount - completedItemCount;
        roomText.text = diff.ToString();
    }
    private void NotifyLevelManager()
    {
        LevelManager.Instance.CompleteRoom();
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
