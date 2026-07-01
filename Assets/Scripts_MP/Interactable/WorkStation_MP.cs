using Interactions;
using ItemScript;
using GameData;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class WorkStation_MP : NetworkBehaviour, IInteractable
{
    [Header("Configuration")]
    [SerializeField] public List<ProcessingRecipe> validRecipes;

    [Header("Visuals")]
    [SerializeField] private Transform placementPoint;
    [SerializeField] private Slider progressBar;

    [Header("Wood")]
    [SerializeField] private Transform putTableHere;

    [Header("Power")]
    [SerializeField] private Generator linkedGenerator;

    [Header("Work Settings")]
    [SerializeField] private float workSpeed = 2f;
    [SerializeField] private float workTickRate = 0.05f;

    // SyncVar'lar: server'da değişir, tüm client'lara otomatik yayılır
    [SyncVar(hook = nameof(OnOccupiedChanged))]
    private bool isOccupied = false;

    [SyncVar(hook = nameof(OnRecipeChanged))]
    private int activeRecipeIndex = -1;

    [SyncVar(hook = nameof(OnProgressChanged))]
    private float currentProgress = 0f;

    private CarriableObject_MP currentHeldItem;
    private Coroutine workRoutine;

    private void Start()
    {
        UpdateProgressUI();
    }

    // --- IInteractable ---

    public void Interact()
    {
        if (linkedGenerator != null && !linkedGenerator.IsRunning)
        {
            Debug.Log("No power!");
            return;
        }
        Debug.Log("WorkStation interacted");
    }

    // --- PlayerInteract_MP tarafından çağrılır ---

    public void RequestHoldWork() => CmdRequestHoldWork();
    public void RequestStopWork() => CmdRequestStopWork();

    [Command(requiresAuthority = false)]
    private void CmdRequestHoldWork()
    {
        if (!isOccupied) return;
        if (linkedGenerator != null && !linkedGenerator.IsRunning) return;
        if (activeRecipeIndex == -1) return;
        if (workRoutine == null)
            workRoutine = StartCoroutine(WorkRoutine());
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestStopWork() => StopWorkRoutine();

    private void StopWorkRoutine()
    {
        if (workRoutine != null)
        {
            StopCoroutine(workRoutine);
            workRoutine = null;
        }
    }

    // Sadece server'da çalışır (Command'dan başlatıldığı için)
    private IEnumerator WorkRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(workTickRate);

        while (isOccupied && activeRecipeIndex != -1)
        {
            if (linkedGenerator != null && !linkedGenerator.IsRunning)
            {
                StopWorkRoutine();
                yield break;
            }

            ProcessingRecipe recipe = validRecipes[activeRecipeIndex];
            currentProgress += workTickRate * workSpeed;

            if (currentProgress >= recipe.workDuration)
            {
                CompleteRecipe(recipe);
                yield break;
            }

            yield return wait;
        }

        workRoutine = null;
    }

    // --- PlaceItem ---

    public int GetRecipeIndexForMaterial(MaterialType mat)
    {
        for (int i = 0; i < validRecipes.Count; i++)
        {
            if (validRecipes[i].inputMaterial == mat)
                return i;
        }
        return -1;
    }

    // PlayerInteract_MP artık bu metodu çağıracak (PlaceItem yerine)
    [Command(requiresAuthority = false)]
    public void CmdPlaceItem(NetworkIdentity itemIdentity, int recipeIndex)
    {
        if (itemIdentity == null) return;

        CarriableObject_MP item = itemIdentity.GetComponent<CarriableObject_MP>();
        if (item == null) return;
        if (recipeIndex < 0 || recipeIndex >= validRecipes.Count) return;
        if (putTableHere == null) return;

        currentHeldItem = item;
        isOccupied = true;
        activeRecipeIndex = recipeIndex;
        currentProgress = 0f;

        // Görsel yerleştirmeyi tüm client'lara yay
        RpcPlaceItemVisual(itemIdentity.gameObject, putTableHere.position, putTableHere.rotation);
    }

    [ClientRpc]
    private void RpcPlaceItemVisual(GameObject itemObj, Vector3 pos, Quaternion rot)
    {
        if (itemObj == null) return;

        Rigidbody rb = itemObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider itemCol = itemObj.GetComponent<Collider>();
        if (itemCol != null)
            itemCol.enabled = false;

        Vector3 originalScale = itemObj.transform.localScale;

        if (putTableHere != null)
            itemObj.transform.SetParent(putTableHere, false);

        itemObj.transform.localPosition = Vector3.zero;
        itemObj.transform.localRotation = Quaternion.identity;
        itemObj.transform.localScale = originalScale;
        itemObj.transform.SetPositionAndRotation(pos, rot);
    }

    // --- CompleteRecipe ---

    private void CompleteRecipe(ProcessingRecipe recipe)
    {
        StopWorkRoutine();

        if (currentHeldItem != null)
        {
            NetworkServer.Destroy(currentHeldItem.gameObject); // SP: Destroy()
            currentHeldItem = null;
        }

        for (int i = 0; i < recipe.outputCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.2f, 0.2f),
                0.2f,
                Random.Range(-0.2f, 0.2f)
            );

            GameObject output = Instantiate(
                recipe.outputPrefab,
                placementPoint.position + offset,
                Quaternion.identity
            );

            NetworkServer.Spawn(output); // SP'de yoktu, tüm clientlara bildir
        }

        isOccupied = false;
        activeRecipeIndex = -1;
        currentProgress = 0f;
    }

    // --- SyncVar hook'ları: değer değişince UI'ı güncelle ---

    private void OnOccupiedChanged(bool oldVal, bool newVal) => UpdateProgressUI();
    private void OnRecipeChanged(int oldVal, int newVal) => UpdateProgressUI();
    private void OnProgressChanged(float oldVal, float newVal) => UpdateProgressUI();

    private void UpdateProgressUI()
    {
        if (progressBar == null) return;

        if (activeRecipeIndex >= 0 && activeRecipeIndex < validRecipes.Count)
        {
            float maxTime = validRecipes[activeRecipeIndex].workDuration;
            progressBar.gameObject.SetActive(true);
            progressBar.value = maxTime > 0f ? currentProgress / maxTime : 0f;
        }
        else
        {
            progressBar.gameObject.SetActive(false);
            progressBar.value = 0f;
        }
    }

    private void OnDisable()
    {
        StopWorkRoutine();
    }
}