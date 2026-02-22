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
        if (progressBar != null)
        {
            if (activeRecipeIndex != -1)
            {
                float maxTime = validRecipes[activeRecipeIndex].workDuration;
                progressBar.value = newVal / maxTime;
                progressBar.gameObject.SetActive(newVal > 0);
            }
            else
            {
                progressBar.gameObject.SetActive(false);
            }
        }
    }

    public bool Interact(IPickupable heldItem)
    {
        // 1. PLACE ITEM (If hands full, station empty)
        if (heldItem != null && !isOccupied)
        {
            CarriableObject obj = heldItem as CarriableObject;
            if (obj == null) return false;

            // Check if this item matches ANY recipe in our list
            int recipeIdx = GetRecipeIndexForMaterial(obj.Material);

            if (recipeIdx != -1)
            {
                CmdPlaceItem(obj.netId, recipeIdx);
                return true;
            }
            return false; // This station doesn't accept this item
        }

        // 2. DO WORK (If hands empty/tool, station occupied)
        if (isOccupied)
        {
            // Optional: Check if player is holding the required tool defined in the recipe
            Tools heldToolType = heldItem != null ? heldItem.Tool : Tools.None;

            CmdRequestWork();
            return true;
        }

        return false;
    }

    private int GetRecipeIndexForMaterial(MaterialType mat)
    {
        for (int i = 0; i < validRecipes.Count; i++)
        {
            if (validRecipes[i].inputMaterial == mat) return i;
        }
        return -1;
    }

    [Command(requiresAuthority = false)]
    private void CmdPlaceItem(uint objectNetId, int recipeIndex)
    {

        if (NetworkServer.spawned.TryGetValue(objectNetId, out NetworkIdentity netObj))
        {
            currentHeldItem = netObj.GetComponent<CarriableObject>();

            // Lock physics
            Rigidbody rb = currentHeldItem.GetComponent<Rigidbody>();
            if (rb)
            {
                // Önce hızları sıfırla (Unity 6 kuralı)
                // rb.linearVelocity = Vector3.zero;
                // rb.angularVelocity = Vector3.zero;

                // Sonra Kinematic yap
                rb.isKinematic = true;
            }

            // Snap position
            currentHeldItem.transform.position = placementPoint.position;
            currentHeldItem.transform.rotation = placementPoint.rotation;

            // Update State
            isOccupied = true;
            activeRecipeIndex = recipeIndex;
            currentProgress = 0f;
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestWork()
    {
        if (activeRecipeIndex == -1) return;

        ProcessingRecipe recipe = validRecipes[activeRecipeIndex];

        // Increment Progress
        currentProgress += 0.5f; // Adjust "Work Speed" here

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
    }
}