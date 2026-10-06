using TMPro;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private TextMeshProUGUI objectiveHintText;

    private int currentObjectiveIndex;
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
                objectiveText.text = "Carry the wood and interact with the highlighted station to place the object.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 3:
                objectiveText.text = "Keep interaction pressed while looking at the station to process the object.";
                objectiveHintText.text = "Interaction Input: E";
                break;
            case 4:
                objectiveText.text = "Carry the processed good to highlighted tile, then interact with the tile.";
                objectiveHintText.text = "Interaction Input: E (While Empty) - Grab, Interaction Input: E (While Carrying)";
                break;
        }
    }
    public void CompleteObjective(int index)
    {
        if (index != currentObjectiveIndex) return;
        currentObjectiveIndex++;
        StartObjective(currentObjectiveIndex);
    }
    private void CompleteTutorial()
    {
        Debug.Log("Tutorial Completed");
    }
}