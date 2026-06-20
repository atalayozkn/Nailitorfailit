using Interactions;
using ItemScript;
using GameData;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class WorkStation : NetworkBehaviour, IInteractable
{
    [Header("Configuration")]
    [Tooltip("List all recipes this specific station can handle.")]
    [SerializeField] public List<ProcessingRecipe> validRecipes;

    [Header("Visuals")]
    [SerializeField] private Transform placementPoint;
    [SerializeField] private UnityEngine.UI.Slider progressBar;

    [Header("Wood")]
    [SerializeField] private Transform putTableHere;
    private bool justPlacedItem = false;

    [Header("Power")]
    [SerializeField] private Generator linkedGenerator;

    [Header("Work Settings")]
    [SerializeField] private float workSpeed = 2f;
    [SerializeField] private float workTickRate = 0.05f;

    [SyncVar] private int activeRecipeIndex = -1;

    [SyncVar(hook = nameof(OnProgressChanged))]
    private float currentProgress = 0f;

    [SyncVar] private bool isOccupied = false;

    private CarriableObject currentHeldItem;
    private Coroutine workRoutine;

    private void Start()
    {
        UpdateProgressUI(currentProgress);
    }

    private void OnProgressChanged(float oldVal, float newVal)
    {
        UpdateProgressUI(newVal);
    }

    private void UpdateProgressUI(float progressValue)
    {
        if (progressBar == null) return;

        if (activeRecipeIndex != -1)
        {
            float maxTime = validRecipes[activeRecipeIndex].workDuration;

            progressBar.gameObject.SetActive(true);
            progressBar.value = maxTime > 0f ? progressValue / maxTime : 0f;
        }
        else
        {
            progressBar.gameObject.SetActive(false);
            progressBar.value = 0f;
        }
    }

    public void Interact()
    {
        if (linkedGenerator != null && !linkedGenerator.IsRunning)
        {
            Debug.Log("No power!");
            return;
        }

        Debug.Log("WorkStation çalıştı");
    }

    public void RequestHoldWork()
    {
        if (!isOccupied) return;

        CmdRequestWork();
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

    [Command(requiresAuthority = false)]
    public void CmdPlaceItem(uint objectNetId, int recipeIndex)
    {
        if (putTableHere == null)
        {
            Debug.LogError("PutTableHere is not assigned!");
            return;
        }

        if (recipeIndex < 0 || recipeIndex >= validRecipes.Count)
            return;

        Debug.Log("WORKSTATION PLACE ITEM CALLED");

        if (!NetworkServer.spawned.TryGetValue(objectNetId, out NetworkIdentity netObj))
            return;

        currentHeldItem = netObj.GetComponent<CarriableObject>();
        if (currentHeldItem == null) return;

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

        UpdateProgressUI(currentProgress);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestWork()
    {
        if (!isOccupied) return;

        if (linkedGenerator != null && !linkedGenerator.IsRunning)
            return;

        if (activeRecipeIndex == -1)
            return;

        if (workRoutine == null)
        {
            workRoutine = StartCoroutine(ServerWorkRoutine());
        }
    }

    private IEnumerator ServerWorkRoutine()
    {
        while (isOccupied && activeRecipeIndex != -1)
        {
            if (linkedGenerator != null && !linkedGenerator.IsRunning)
            {
                workRoutine = null;
                yield break;
            }

            ProcessingRecipe recipe = validRecipes[activeRecipeIndex];

            currentProgress += workTickRate * workSpeed;

            if (currentProgress >= recipe.workDuration)
            {
                CompleteRecipe(recipe);
                workRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(workTickRate);
        }

        workRoutine = null;
    }

    private void CompleteRecipe(ProcessingRecipe recipe)
    {
        if (currentHeldItem != null)
        {
            NetworkServer.Destroy(currentHeldItem.gameObject);
        }

        for (int i = 0; i < recipe.outputCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.2f, 0.2f),
                0.2f,
                Random.Range(-0.2f, 0.2f)
            );

            GameObject product = Instantiate(
                recipe.outputPrefab,
                placementPoint.position + offset,
                Quaternion.identity
            );

            NetworkServer.Spawn(product);
        }

        isOccupied = false;
        activeRecipeIndex = -1;
        currentProgress = 0f;
        currentHeldItem = null;
        justPlacedItem = false;

        UpdateProgressUI(currentProgress);
    }


    public void RequestStopWork()
    {
        CmdStopWork();
    }

    [Command(requiresAuthority = false)]
    private void CmdStopWork()
    {
        StopWorkRoutine();
    }

    private void StopWorkRoutine()
    {
        if (workRoutine != null)
        {
            StopCoroutine(workRoutine);
            workRoutine = null;
        }
    }
}