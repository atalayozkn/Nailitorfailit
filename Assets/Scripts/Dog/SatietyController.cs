using System.Collections;
using UnityEngine;
public class SatietyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DogStateMachine stateMachine;
    [SerializeField] private UIImageFillHelper fillHelper;

    [Header("Settings")]
    [SerializeField] private float maxSatiety = 100f;
    [SerializeField] private float hungerInterval = 2.0f;
    private float currentSatiety;
    
    private Coroutine hungerRoutine;
    private void OnEnable()
    {
        currentSatiety = maxSatiety;
        UpdateUI();

        if (hungerRoutine != null)
        {
            StopCoroutine(hungerRoutine);
            hungerRoutine = null;
        }

        hungerRoutine = StartCoroutine(HungerTick());
    }
    private void OnDisable()
    {
        StopCoroutine(hungerRoutine);
        hungerRoutine = null;
    }
    private IEnumerator HungerTick()
    {
        while (true)
        {
            currentSatiety--;
            UpdateUI();

            if (currentSatiety == 60) stateMachine.ChangeToEatState();
            if (currentSatiety == 40) stateMachine.ChangeToEatState();
            if (currentSatiety == 20) stateMachine.ChangeToEatState();
            if (currentSatiety == 10) stateMachine.ChangeToEatState();

            yield return new WaitForSeconds(hungerInterval);
        }
    }
    private void UpdateUI()
    {
        float percent = (currentSatiety / maxSatiety);
        fillHelper.UpdateUI(percent);
    }
    public void GainSatiety(int amount)
    {
        currentSatiety += amount;
    }
}
