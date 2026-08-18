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
        ResetProcess();
    }

    public void Process()
    {
        if (isCompleted)
        {
            return;
        }

        currentProcess += processPerInteract;

        if (currentProcess >= maxProcessAmount)
        {
            currentProcess = maxProcessAmount;
            isCompleted = true;
        }

        canvasHelper.UpdateUI(currentProcess);

        if (isCompleted)
        {
            canvasHelper.SetActivity(false);
        }
    }

    public void ResetProcess()
    {
        currentProcess = 0f;
        isCompleted = false;

        canvasHelper.SetUIProperties(maxProcessAmount);
        canvasHelper.UpdateUI(0f);
        canvasHelper.SetActivity(true);
    }

    public bool IsCompleted()
    {
        return isCompleted;
    }
}