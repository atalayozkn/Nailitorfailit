using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EmoteWheel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private List<EmoteTab> emoteTabs;
    [SerializeField] private InputActionReference nextIndexAction;
    [SerializeField] private InputActionReference previousIndexAction;

    private EmoteTab currentTab;
    private int currentIndex;
    private bool isActive;

    private void OnEnable()
    {
        isActive = false;
        currentIndex = 0;
        currentTab = null;
        canvasRoot.SetActive(false);
        nextIndexAction.action.Enable();
        previousIndexAction.action.Enable();

        // CloseVisuals();
    }

    private void OnDisable()
    {
        nextIndexAction.action.Disable();
        previousIndexAction.action.Disable();
    }

    private void Update()
    {
        if (!isActive) return;

        if (nextIndexAction.action.WasPressedThisFrame()) IncrementIndex();
        if (previousIndexAction.action.WasPressedThisFrame()) ReductIndex();
    }

    private void IncrementIndex()
    {
        int previousIndex = currentIndex;
        currentIndex++;
        if (currentIndex > emoteTabs.Count + 1) currentIndex = emoteTabs.Count + 1;
        UpdateSelection(previousIndex);
    }

    private void ReductIndex()
    {
        int previousIndex = currentIndex;
        currentIndex--;
        if (currentIndex < 0) currentIndex = 0;
        UpdateSelection(previousIndex);
    }

    private void UpdateSelection(int previousIndex)
    {
        if (previousIndex > 0 && previousIndex <= emoteTabs.Count) emoteTabs[previousIndex - 1].OnHoverOff();

        if (currentIndex > 0 && currentIndex <= emoteTabs.Count)
        {
            currentTab = emoteTabs[currentIndex - 1];
            currentTab.OnHoverOn();
        }
        else currentTab = null;
    }

    public void SetActivity(bool condition)
    {
        if (isActive == condition) return;

        isActive = condition;

        if (!condition)
        {
            if (currentTab != null)
            {
                currentTab.Execute();
                currentTab.OnHoverOff();
            }

            currentTab = null;
            currentIndex = 0;
        }

        canvasRoot.SetActive(condition);
    }
}