using UnityEngine;

public class InteractionProcessHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InteractionUIHelper canvasHelper;

    [Header("Process Settings")]
    [SerializeField] private float maxProcessAmount = 100f;
    [SerializeField] private float processPerInteract = 25f;
    // Tiles show their bar as soon as they are placed. The cutter keeps it hidden until material is loaded (ResetProcess).
    [SerializeField] private bool showBarOnEnable = true;

    public float NormalizedProgress => maxProcessAmount > 0f ? Mathf.Clamp01(currentProcess / maxProcessAmount) : 0f;

    private float currentProcess;
    private bool isCompleted;

    // This helper alone decides the bar's visibility, so the OnEnable order against InteractionUIHelper doesn't matter.
    private void OnEnable()
    {
        ResetProcess();
        if (!showBarOnEnable) canvasHelper.SetActivity(false);
    }
    public void Process()
    {
        if (isCompleted) return;

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