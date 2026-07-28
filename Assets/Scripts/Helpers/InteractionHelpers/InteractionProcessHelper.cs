using UnityEngine;
using UnityEngine.Events;

public class InteractionProcessHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InteractionUIHelper canvasHelper;

    [Header("Process Settings")]
    [SerializeField] private float maxProcessAmount = 100f;
    [SerializeField] private float processPerInteract = 25f;

    [Header("Events")]
    [SerializeField] private UnityEvent onConstructionStarted;
    [SerializeField] private UnityEvent onConstructionProgress;
    [SerializeField] private UnityEvent onConstructionCompleted;

    private float currentProcess;
    private bool isCompleted;
    private void OnEnable()
    {
        isCompleted = false;
        canvasHelper.SetUIProperties(maxProcessAmount);
    }
    public void Process()
    {
        currentProcess += processPerInteract;
        canvasHelper.UpdateUI(currentProcess);

        if (currentProcess >= maxProcessAmount)
        {
            currentProcess = maxProcessAmount;
            isCompleted = true;
            canvasHelper.SetActivity(false);
        }
    }
    public bool IsCompleted()
    {
        return isCompleted;
    }
}
