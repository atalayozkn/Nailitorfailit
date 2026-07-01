// ============================================================
// File:    PlayerInteract_MP.cs
// Author:  Tarık + Murad
// Created: 30-Jun-2026
// Purpose: Handles player interaction with objects in a multiplayer game using Mirror networking
// ============================================================

using System.Collections;
using Interactions;
using ItemScript;
using Mirror;
using PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract_MP : NetworkBehaviour
{
    [SerializeField] private InputActionReference interactAction;

    [Header("Carry")]
    private Rigidbody carriedRb;
    private IPickupable currentCarriable;
    private GameObject currentObj;

    [SerializeField] private Transform holdPoint;
    public bool IsCarrying => currentCarriable != null;

    [Header("Cooldown")]
    private float dropCooldown = 0.2f;
    private float lastDropTime;

    [Header("Highlight")]
    [SerializeField] private float highlightRedAmount = 0.9f;

    private Renderer highlightedRenderer;
    private Material highlightedMaterial;
    private Color highlightedOriginalColor;

    [Header("DEBUG")]
    [SerializeField] private GameObject currentTarget;

    private Coroutine holdWorkRoutine;
    private bool interactHeld;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (interactAction != null && interactAction.action != null)
            interactAction.action.Enable();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        if (!IsCarrying || carriedRb == null) return;

        carriedRb.MovePosition(holdPoint.position);
        carriedRb.MoveRotation(holdPoint.rotation);
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        if (interactAction == null || interactAction.action == null)
            return;

        if (interactAction.action.WasPressedThisFrame())
        {
            interactHeld = true;
            HandleInteractPressed();

            if (!IsCarrying && holdWorkRoutine == null)
                holdWorkRoutine = StartCoroutine(HoldWorkRoutine());
        }

        if (interactAction.action.WasReleasedThisFrame())
        {
            interactHeld = false;
            StopHoldWorkRoutine();

            if (TryGetWorkStation(out WorkStation_MP station))
                station.RequestStopWork();
        }
    }

    private IEnumerator HoldWorkRoutine()
    {
        while (interactHeld)
        {
            if (!IsCarrying && TryGetWorkStation(out WorkStation_MP station))
                station.RequestHoldWork();

            yield return null;
        }

        holdWorkRoutine = null;
    }

    private void StopHoldWorkRoutine()
    {
        if (holdWorkRoutine != null)
        {
            StopCoroutine(holdWorkRoutine);
            holdWorkRoutine = null;
        }
    }

    private void HandleInteractPressed()
    {
        if (IsCarrying)
        {
            if (TryGetWorkStation(out WorkStation_MP station))
            {
                PlaceToStation(station);
                return;
            }

            if (currentTarget != null)
            {
                InteractWithTarget(currentTarget);
                return;
            }

            Drop();
            return;
        }

        if (currentTarget != null)
            InteractWithTarget(currentTarget);
    }

    public IPickupable GetCurrentItem() => currentCarriable;

    private void OnTriggerEnter(Collider other)
    {
        if (!isLocalPlayer) return;

        if (Time.time - lastDropTime < dropCooldown)
            return;

        ConstructObject_MP construct = other.GetComponentInParent<ConstructObject_MP>();
        if (construct != null)
        {
            SetCurrentTarget(construct.gameObject);
            Debug.Log("Construct target girildi: " + currentTarget.name);
            return;
        }

        WorkStation_MP station = other.GetComponentInParent<WorkStation_MP>();
        if (station != null)
        {
            SetCurrentTarget(station.gameObject);
            Debug.Log("WorkStation target girildi: " + currentTarget.name);
            return;
        }

        CarriableObject_MP carriable = other.GetComponentInParent<CarriableObject_MP>();
        if (carriable != null)
        {
            SetCurrentTarget(carriable.gameObject);
            Debug.Log("Pickup target girildi: " + currentTarget.name);
            return;
        }

        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable is MonoBehaviour mb)
        {
            SetCurrentTarget(mb.gameObject);
            Debug.Log("Interactable target girildi: " + currentTarget.name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isLocalPlayer) return;

        GameObject exitedTarget = GetTargetFromCollider(other);

        if (exitedTarget != null && exitedTarget == currentTarget)
        {
            ClearCurrentTarget();
            Debug.Log("Target çıkıldı");
        }
    }

    private GameObject GetTargetFromCollider(Collider other)
    {
        ConstructObject_MP construct = other.GetComponentInParent<ConstructObject_MP>();
        if (construct != null) return construct.gameObject;

        WorkStation_MP station = other.GetComponentInParent<WorkStation_MP>();
        if (station != null) return station.gameObject;

        CarriableObject_MP carriable = other.GetComponentInParent<CarriableObject_MP>();
        if (carriable != null) return carriable.gameObject;

        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable is MonoBehaviour mb) return mb.gameObject;

        return null;
    }

    private void SetCurrentTarget(GameObject newTarget)
    {
        if (currentTarget == newTarget) return;

        RestoreHighlight();
        currentTarget = newTarget;
        ApplyHighlight(currentTarget);
    }

    private void ClearCurrentTarget()
    {
        RestoreHighlight();
        currentTarget = null;
    }

    private void ApplyHighlight(GameObject target)
    {
        if (target == null) return;

        highlightedRenderer = target.GetComponent<Renderer>();

        if (highlightedRenderer == null)
            highlightedRenderer = target.GetComponentInChildren<Renderer>(true);

        if (highlightedRenderer == null) return;

        Material[] mats = highlightedRenderer.materials;
        if (mats == null || mats.Length == 0) return;

        highlightedMaterial = mats[0];
        highlightedOriginalColor = highlightedMaterial.color;

        highlightedMaterial.color = Color.Lerp(
            highlightedOriginalColor,
            Color.red,
            highlightRedAmount
        );
    }

    private void RestoreHighlight()
    {
        if (highlightedMaterial != null)
            highlightedMaterial.color = highlightedOriginalColor;

        highlightedRenderer = null;
        highlightedMaterial = null;
    }

    private bool TryGetWorkStation(out WorkStation_MP station)
    {
        station = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);

        foreach (var hit in hits)
        {
            station = hit.GetComponentInParent<WorkStation_MP>();
            if (station != null) return true;
        }

        return false;
    }

    private void Pickup(GameObject obj)
    {
        var item = obj.GetComponent<IPickupable>();
        if (item == null) return;

        ClearCurrentTarget();

        currentCarriable = item;
        currentObj = obj;
        carriedRb = item.GetRigidbody();

        if (carriedRb == null) return;

        carriedRb.transform.SetParent(null);
        currentCarriable.OnPickUp();
        carriedRb.isKinematic = true;
        carriedRb.transform.position = holdPoint.position;
        carriedRb.transform.rotation = holdPoint.rotation;

        Collider playerCollider = GetComponentInParent<Collider>();
        Collider itemCollider = carriedRb.GetComponent<Collider>();
        if (playerCollider != null && itemCollider != null)
            Physics.IgnoreCollision(playerCollider, itemCollider, true);

        NetworkIdentity itemIdentity = obj.GetComponent<NetworkIdentity>();
        if (itemIdentity != null)
            CmdAssignAuthority(itemIdentity);

        Debug.Log("Item alındı: " + obj.name);
    }

    private void Drop()
    {
        if (!IsCarrying) return;

        NetworkIdentity itemIdentity = currentObj?.GetComponent<NetworkIdentity>();
        if (itemIdentity != null)
            CmdRemoveAuthority(itemIdentity);

        currentCarriable.OnDrop();

        if (carriedRb != null)
        {
            carriedRb.isKinematic = false;

            Collider playerCollider = GetComponentInParent<Collider>();
            Collider itemCollider = carriedRb.GetComponent<Collider>();
            if (playerCollider != null && itemCollider != null)
                Physics.IgnoreCollision(playerCollider, itemCollider, false);
        }

        ClearCurrentTarget();

        currentCarriable = null;
        currentObj = null;
        carriedRb = null;
        lastDropTime = Time.time;

        Debug.Log("Item bırakıldı");
    }

    private void PlaceToStation(WorkStation_MP station)
    {
        if (station == null) return;
        if (currentObj == null) return;

        CarriableObject_MP carriable = currentObj.GetComponent<CarriableObject_MP>();
        if (carriable == null) return;

        int recipeIndex = station.GetRecipeIndexForMaterial(carriable.Material);
        if (recipeIndex == -1)
        {
            Debug.Log("Bu materyal burada kullanılamaz");
            return;
        }

        NetworkIdentity itemIdentity = carriable.GetComponent<NetworkIdentity>();
        if (itemIdentity == null)
        {
            Debug.LogError("CarriableObject_MP üzerinde NetworkIdentity yok!");
            return;
        }

        GameObject placedObj = currentObj;
        Rigidbody placedRb = carriedRb;

        ClearCurrentTarget();

        if (placedRb != null)
        {
            Collider playerCollider = GetComponentInParent<Collider>();
            Collider itemCollider = placedRb.GetComponent<Collider>();
            if (playerCollider != null && itemCollider != null)
                Physics.IgnoreCollision(playerCollider, itemCollider, false);
        }

        currentObj = null;
        carriedRb = null;
        currentCarriable = null;

        CmdRemoveAuthority(itemIdentity);
        station.CmdPlaceItem(itemIdentity, recipeIndex);

        Debug.Log("Item WorkStation'a yerleştirildi: " + placedObj.name);
    }

    private void InteractWithTarget(GameObject target)
    {
        if (target == null) return;

        if (target.CompareTag("Pickup"))
        {
            if (IsCarrying) return;
            Pickup(target);
            return;
        }

        ConstructObject_MP construct = target.GetComponentInParent<ConstructObject_MP>();

        if (construct != null)
        {
            if (!IsCarrying) return;
            if (currentObj == null) return;

            CarriableObject_MP carriedObj = currentObj.GetComponent<CarriableObject_MP>();
            if (carriedObj == null) return;

            NetworkIdentity itemIdentity = carriedObj.GetComponent<NetworkIdentity>();
            if (itemIdentity == null) return;

            bool built = false;

            try
            {
                built = construct.TryBuild(carriedObj);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Construct build sırasında hata oluştu: " + e.Message);
                return;
            }

            if (!built) return;

            ReleaseHeldItemForStation();
            CmdDestroyObject(itemIdentity);

            Debug.Log("Build tamamlandı");
            return;
        }

        var interactable = target.GetComponent<IInteractable>();
        interactable?.Interact();
    }

    private void ReleaseHeldItemForStation()
    {
        if (!IsCarrying) return;

        if (carriedRb != null)
        {
            Collider playerCollider = GetComponentInParent<Collider>();
            Collider itemCollider = carriedRb.GetComponent<Collider>();
            if (playerCollider != null && itemCollider != null)
                Physics.IgnoreCollision(playerCollider, itemCollider, false);
        }

        NetworkIdentity itemIdentity = currentObj?.GetComponent<NetworkIdentity>();
        if (itemIdentity != null)
            CmdRemoveAuthority(itemIdentity);

        ClearCurrentTarget();

        carriedRb = null;
        currentCarriable = null;
        currentObj = null;
    }

    // --- Authority Komutları ---

    [Command]
    private void CmdAssignAuthority(NetworkIdentity itemIdentity)
    {
        if (itemIdentity == null) return;

        if (itemIdentity.connectionToClient != null)
            itemIdentity.RemoveClientAuthority();

        itemIdentity.AssignClientAuthority(connectionToClient);
    }

    [Command]
    private void CmdRemoveAuthority(NetworkIdentity itemIdentity)
    {
        if (itemIdentity == null) return;
        if (itemIdentity.connectionToClient != null)
            itemIdentity.RemoveClientAuthority();
    }

    [Command]
    private void CmdDestroyObject(NetworkIdentity identity)
    {
        if (identity == null) return;
        NetworkServer.Destroy(identity.gameObject);
    }

    private void OnDisable()
    {
        StopHoldWorkRoutine();
        RestoreHighlight();
    }
}