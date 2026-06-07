using Interactions;
using ItemScript;
using GameData;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WorkStation_SP : MonoBehaviour, IInteractable
{
    [Header("Configuration")]
    [Tooltip("List all recipes this specific station can handle.")]
    [SerializeField] public List<ProcessingRecipe> validRecipes;

    [Header("Visuals")]
    [SerializeField] private Transform placementPoint;
    [SerializeField] private Slider progressBar;

    [Header("Wood")]
    [SerializeField] private Transform putTableHere;
    private bool justPlacedItem = false;

    [Header("Power")]
    [SerializeField] private Generator linkedGenerator;

    private int activeRecipeIndex = -1;
    private float currentProgress = 0f;
    private bool isOccupied = false;

    private CarriableObject_SP currentHeldItem;

    private void Start()
    {
        UpdateProgressUI();
    }

    public void Interact()
    {
        if (linkedGenerator != null && !linkedGenerator.IsRunning)
        {
            Debug.Log("No power!");
            return;
        }

        Debug.Log("WorkStation çalýþtý");
    }

    public void RequestHoldWork()
    {
        if (!isOccupied) return;

        RequestWork();
    }

    public int GetRecipeIndexForMaterial(MaterialType mat)
    {
        for (int i = 0; i < validRecipes.Count; i++)
        {
            if (validRecipes[i].inputMaterial == mat)
                return i;
        }

        return -1;
    }

    public void PlaceItem(CarriableObject_SP item, int recipeIndex)
    {
        if (putTableHere == null)
        {
            Debug.LogError("PutTableHere is not assigned!");
            return;
        }

        if (item == null)
            return;

        if (recipeIndex < 0 || recipeIndex >= validRecipes.Count)
            return;

        Debug.Log("WORKSTATION PLACE ITEM CALLED");

        currentHeldItem = item;

        Rigidbody rb = currentHeldItem.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider itemCol = currentHeldItem.GetComponent<Collider>();
        if (itemCol != null)
            itemCol.enabled = false;

        Vector3 originalScale = currentHeldItem.transform.localScale;

        currentHeldItem.transform.SetParent(putTableHere, false);
        currentHeldItem.transform.localPosition = Vector3.zero;
        currentHeldItem.transform.localRotation = Quaternion.identity;
        currentHeldItem.transform.localScale = originalScale;

        currentHeldItem.transform.SetPositionAndRotation(
            putTableHere.position,
            putTableHere.rotation
        );

        isOccupied = true;
        activeRecipeIndex = recipeIndex;
        currentProgress = 0f;

        justPlacedItem = true;

        UpdateProgressUI();
    }

    private void RequestWork()
    {
        if (linkedGenerator != null && !linkedGenerator.IsRunning)
            return;

        if (activeRecipeIndex == -1)
            return;

        ProcessingRecipe recipe = validRecipes[activeRecipeIndex];

        currentProgress += Time.deltaTime * 2f;

        if (currentProgress >= recipe.workDuration)
        {
            CompleteRecipe(recipe);
        }

        UpdateProgressUI();
    }

    private void CompleteRecipe(ProcessingRecipe recipe)
    {
        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem.gameObject);
        }

        for (int i = 0; i < recipe.outputCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.2f, 0.2f),
                0.2f,
                Random.Range(-0.2f, 0.2f)
            );

            Instantiate(
                recipe.outputPrefab,
                placementPoint.position + offset,
                Quaternion.identity
            );
        }

        isOccupied = false;
        activeRecipeIndex = -1;
        currentProgress = 0f;
        currentHeldItem = null;
        justPlacedItem = false;

        UpdateProgressUI();
    }

    private void UpdateProgressUI()
    {
        if (progressBar == null) return;

        if (activeRecipeIndex != -1)
        {
            float maxTime = validRecipes[activeRecipeIndex].workDuration;

            progressBar.gameObject.SetActive(true);
            progressBar.value = currentProgress / maxTime;
        }
        else
        {
            progressBar.gameObject.SetActive(false);
            progressBar.value = 0f;
        }
    }
}
