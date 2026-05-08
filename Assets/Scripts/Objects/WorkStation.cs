using Interactions;
using ItemScript;
using GameData;
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

    // Network State
    // We store the index of the recipe currently being processed (-1 if none)
    /* WITH THE MIRROR SYSTEM IT HAS BEEN CHANGED
    private NetworkVariable<int> activeRecipeIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<float> currentProgress = new NetworkVariable<float>(0f);
    private NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);
    */
    // Eğer değer değişince metod çalışsın istersen: [SyncVar(hook = nameof(OnRecipeChanged))]
    [SyncVar] private int activeRecipeIndex = -1;
    [SyncVar(hook = nameof(OnProgressChanged))] private float currentProgress = 0f;
    [SyncVar] private bool isOccupied = false;
    // Local Reference
    private CarriableObject currentHeldItem;

    /*public override void OnStartClient()
    {
        currentProgress.OnValueChanged += OnProgressChanged;
    }

    public override void OnStopClient()
    {
        currentProgress.OnValueChanged -= OnProgressChanged;
    }*/

    private void OnProgressChanged(float oldVal, float newVal)
    {
        // Update UI locally based on Network Data
        if (progressBar == null) return;

        if (activeRecipeIndex != -1)
        {
            float maxTime = validRecipes[activeRecipeIndex].workDuration;

            progressBar.gameObject.SetActive(true);
            progressBar.value = newVal / maxTime;
        }
        else
        {
            progressBar.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);
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
            if (validRecipes[i].inputMaterial == mat) return i;
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

        // Dünya uzayında da PutTableHere ile birebir eşitle
        currentHeldItem.transform.SetPositionAndRotation(putTableHere.position, putTableHere.rotation);

        isOccupied = true;
        activeRecipeIndex = recipeIndex;
        currentProgress = 0f;

        justPlacedItem = true;
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestWork()
    {
        //CHECK POWER
        if (linkedGenerator != null && !linkedGenerator.IsRunning)
            return;

        if (activeRecipeIndex == -1) return;

        ProcessingRecipe recipe = validRecipes[activeRecipeIndex];

        currentProgress += Time.deltaTime * 2f;

        if (currentProgress >= recipe.workDuration)
        {
            CompleteRecipe(recipe);
        }
    }

    private void CompleteRecipe(ProcessingRecipe recipe)
    {
        // 1. Destroy Input
        if (currentHeldItem != null)
        {
            NetworkServer.Destroy(currentHeldItem.gameObject);
        }

        // 2. Spawn Output(s)
        for (int i = 0; i < recipe.outputCount; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.2f, Random.Range(-0.2f, 0.2f));
            GameObject product = Instantiate(recipe.outputPrefab, placementPoint.position + offset, Quaternion.identity);
            NetworkServer.Spawn(product);
        }

        // 3. Reset Station
        isOccupied = false;
        activeRecipeIndex = -1;
        currentProgress = 0f;
        currentHeldItem = null;
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(false);
        }
    }
}