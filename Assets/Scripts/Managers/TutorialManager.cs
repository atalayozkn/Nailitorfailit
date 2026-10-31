using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private TextMeshProUGUI objectiveHintText;
    [SerializeField] private TutorialObjective[] objectives;
    private int currentObjectiveIndex;

    [SerializeField] private UnityEvent onTutorialEnd;
    // Scene-only singleton: it references this scene's objectives, so it must not survive a scene load
    // (with DontDestroyOnLoad a replayed tutorial kept the old, destroyed objectives).
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    private void Start()
    {
        currentObjectiveIndex = 0;
        StartObjective(currentObjectiveIndex);
    }
    private void StartObjective(int index)
    {
        switch (index)
        {
            case 0:
                objectiveText.text = "Your dog needs a home! Walk to the highlighted spot in the yard.";
                objectiveHintText.text = "Movement Input: W,A,S,D";
                break;
            case 1:
                objectiveText.text = "Interact with the wood pile to pick up a plank.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 2:
                objectiveText.text = "Carry the wood to the Wood Cutter and interact to put it in. Keep interacting to cut it into a floor tile.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 3:
                objectiveText.text = "Carry the cut tile to the highlighted spot of the dog house and interact to place it.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 4:
                objectiveText.text = "Keep interacting with the placed tile to nail it down.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 5:
                objectiveText.text = "Finish all 4 tiles of the dog house floor.";
                objectiveHintText.text = "";
                break;
            case 6:
                objectiveText.text = "";
                objectiveHintText.text = "";
                break;
        }

        objectives[index].gameObject.SetActive(true);
    }
    public void CompleteObjective(int index)
    {
        if (index != currentObjectiveIndex) return;

        // Optional: hide the objective that was just finished
        // objectives[index].gameObject.SetActive(false);

        currentObjectiveIndex++;

        if (currentObjectiveIndex >= objectives.Length)
        {
            CompleteTutorial();
            return;
        }

        StartObjective(currentObjectiveIndex);
    }
    private void CompleteTutorial()
    {
        // Tutorial has no LevelManager, so it saves its own completion (unlocks the next level).
        GameManager.Instance?.CompleteCurrentLevel();
        onTutorialEnd?.Invoke();
        Debug.Log("Tutorial Completed");
    }
}