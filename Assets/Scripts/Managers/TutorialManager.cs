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
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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
                objectiveText.text = "Move to highlighted area.";
                objectiveHintText.text = "Movement Input: W,A,S,D";
                break;
            case 1:
                objectiveText.text = "Interact with the highlighted object to obtain wood.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 2:
                objectiveText.text = "Carry the wood and interact with the highlighted station to place the object. After placing keep interacting with station to process.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 3:
                objectiveText.text = "Carry the processed good to highlighted tile, then interact with the tile. This will place your object onto the construction tile";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 4:
                objectiveText.text = "Keep interaction pressed while looking at the consturction zone to complete construction.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 5:
                objectiveText.text = "Complete all the tiles for the room.";
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
        onTutorialEnd?.Invoke();
        Debug.Log("Tutorial Completed");
    }
}