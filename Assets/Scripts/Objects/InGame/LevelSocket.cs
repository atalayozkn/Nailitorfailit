using Interactions;
using System.Collections;
using TMPro;
using UnityEngine;

public class LevelSocket : MonoBehaviour
{
    private enum SocketPhase
    {
        Available,
        NotAvailable,
        Indication,
        Complete
    }

    [Header("Level")]
    [SerializeField] private int levelIndex;
    [SerializeField] private int currencyCost;

    [Header("References")]
    [SerializeField] private GameObject canvasObject;
    [SerializeField] private TextMeshProUGUI textComponent;

    [Header("Timing")]
    [SerializeField] private float indicationDuration = 2f;

    private SocketPhase currentPhase;

    private void OnEnable()
    {
        canvasObject.SetActive(false);
    }

    public void Refresh(bool isCompleted, bool isIndicated)
    {
        if (isCompleted)
        {
            if (isIndicated)
            {
                SetPhase(SocketPhase.Complete);
            }
            else
            {
                SetPhase(SocketPhase.Indication);
            }

            return;
        }

        if (CurrencyManager.Instance.HasEnoughCurrency(currencyCost)) SetPhase(SocketPhase.Available);
        else SetPhase(SocketPhase.NotAvailable);
    }

    private void SetPhase(SocketPhase phase)
    {
        if (currentPhase == phase) return;

        currentPhase = phase;

        switch (currentPhase)
        {
            case SocketPhase.Available:
                // Available visuals
                break;
            case SocketPhase.NotAvailable:
                // Locked visuals
                break;
            case SocketPhase.Indication:
                StartCoroutine(IndicationRoutine());
                break;
            case SocketPhase.Complete:
                // Completed visuals
                break;
        }
    }

    private IEnumerator IndicationRoutine()
    {
        // TODO
        // Play particles
        // Play animation
        // Play sound

        yield return new WaitForSeconds(indicationDuration);
        GameManager.Instance.MarkLevelIndicated(levelIndex);
        SetPhase(SocketPhase.Complete);
    }
    public void OnInteract()
    {
        if (currentPhase != SocketPhase.Available) return;
        GameManager.Instance.ChangeToLevelPhase(levelIndex);
    }
    public void SetActivity(bool condition)
    {
        canvasObject.SetActive(condition);
        if (currentPhase == SocketPhase.Available)
        {
            textComponent.color = Color.green;
        }
        else 
        {
            textComponent.color = Color.red;
        }
    }
}