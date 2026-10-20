using UnityEngine;
using UnityEngine.UI;

// UI button in the level selection tab. Replaces the old in-world LevelSocket.
[RequireComponent(typeof(Button))]
public class LevelSelectButton : MonoBehaviour
{
    [Header("Level")]
    [Tooltip("Index in GameSceneManager scenes (1 = Tutorial, 2 = Demo ...).")]
    [SerializeField] private int levelIndex;

    [Header("Visuals (optional)")]
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject completedVisual;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }
    private void OnEnable()
    {
        Refresh();
    }
    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    public void Refresh()
    {
        bool exists = GameSceneManager.Instance != null && levelIndex >= 1 && levelIndex <= GameSceneManager.Instance.GetLevelCount();
        bool unlocked = exists && SaveSystem.IsLevelUnlocked(levelIndex);

        button.interactable = unlocked;
        if (lockedVisual != null) lockedVisual.SetActive(!unlocked);
        if (completedVisual != null) completedVisual.SetActive(SaveSystem.IsLevelCompleted(levelIndex));
    }

    private void OnClick()
    {
        if (!SaveSystem.IsLevelUnlocked(levelIndex)) return;
        GameManager.Instance.PlayLevel(levelIndex);
    }
}
