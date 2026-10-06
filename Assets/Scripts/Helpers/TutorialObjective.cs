using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialObjective : MonoBehaviour
{
    [Header("Objective")]
    [SerializeField] private ObjectiveTypes objectiveType;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private InputActionReference objectiveInput;
    [SerializeField] private int completionIndex;
    private void OnEnable()
    {
        SetupObjective(objectiveType);
    }
    private void SetupObjective(ObjectiveTypes type)
    {
        switch (type)
        {
            case ObjectiveTypes.Navigation:
                break;
            case ObjectiveTypes.Input:
                if (objectiveInput != null) objectiveInput.action.Enable();
                break;
            case ObjectiveTypes.ObjectCondition:
                break;
        }
    }
    private void Update()
    {
        if (objectiveType != ObjectiveTypes.Input || objectiveInput == null) return;
        if (objectiveInput.action.WasPressedThisFrame())
        {
            NotifyCompletion();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (objectiveType != ObjectiveTypes.Navigation) return;
        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;
        NotifyCompletion();
    }
    private void NotifyCompletion()
    {
        TutorialManager.Instance.CompleteObjective(completionIndex);
    }
    public void CompleteCondition()
    {
        if (objectiveType != ObjectiveTypes.ObjectCondition) return;
        NotifyCompletion();
    }
    private void OnDisable()
    {
        if (objectiveInput != null) objectiveInput.action.Disable();
    }
}