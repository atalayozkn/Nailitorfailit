using Interactions;
using ItemScript;
using GameData;
using System.Collections.Generic;
using Unity.Netcode;
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
    private NetworkVariable<int> activeRecipeIndex = new NetworkVariable<int>(-1);
    private NetworkVariable<float> currentProgress = new NetworkVariable<float>(0f);
    private NetworkVariable<bool> isOccupied = new NetworkVariable<bool>(false);

    // Local Reference
    private CarriableObject currentHeldItem;

    public override void OnNetworkSpawn()
    {
        currentProgress.OnValueChanged += OnProgressChanged;
    }

    public override void OnNetworkDespawn()
    {
        currentProgress.OnValueChanged -= OnProgressChanged;
    }

    private void OnProgressChanged(float oldVal, float newVal)
    {
        // Update UI locally based on Network Data
        if (progressBar != null)
        {
            if (activeRecipeIndex.Value != -1)
            {
                float maxTime = validRecipes[activeRecipeIndex.Value].workDuration;
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
        if (heldItem != null && !isOccupied.Value)
        {
            CarriableObject obj = heldItem as CarriableObject;
            if (obj == null) return false;

            // Check if this item matches ANY recipe in our list
            int recipeIdx = GetRecipeIndexForMaterial(obj.Material);

            if (recipeIdx != -1)
            {
                PlaceItemServerRpc(obj.NetworkObjectId, recipeIdx);
                return true;
            }
            return false; // This station doesn't accept this item
        }

        // 2. DO WORK (If hands empty/tool, station occupied)
        if (isOccupied.Value)
        {
            // Optional: Check if player is holding the required tool defined in the recipe
            Tools heldToolType = heldItem != null ? heldItem.Tool : Tools.None;

            RequestWorkServerRpc();
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

    [Rpc(SendTo.Server)]
    private void PlaceItemServerRpc(ulong objectId, int recipeIndex)
    {

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
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
            isOccupied.Value = true;
            activeRecipeIndex.Value = recipeIndex;
            currentProgress.Value = 0f;
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestWorkServerRpc()
    {
        if (activeRecipeIndex.Value == -1) return;

        ProcessingRecipe recipe = validRecipes[activeRecipeIndex.Value];

        // Increment Progress
        currentProgress.Value += 0.5f; // Adjust "Work Speed" here

        if (currentProgress.Value >= recipe.workDuration)
        {
            CompleteRecipe(recipe);
        }
    }

    private void CompleteRecipe(ProcessingRecipe recipe)
    {
        // 1. Destroy Input
        if (currentHeldItem != null)
        {
            currentHeldItem.GetComponent<NetworkObject>().Despawn(true);
        }

        // 2. Spawn Output(s)
        for (int i = 0; i < recipe.outputCount; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.2f, Random.Range(-0.2f, 0.2f));
            GameObject product = Instantiate(recipe.outputPrefab, placementPoint.position + offset, Quaternion.identity);
            product.GetComponent<NetworkObject>().Spawn();
        }

        // 3. Reset Station
        isOccupied.Value = false;
        activeRecipeIndex.Value = -1;
        currentProgress.Value = 0f;
        currentHeldItem = null;
    }
}