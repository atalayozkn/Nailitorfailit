using System.Collections;
using Interactions;
using ItemScript;
using PlayerScripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteract_SP : MonoBehaviour
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

    private void FixedUpdate()
    {
        if (!IsCarrying || carriedRb == null) return;

        carriedRb.MovePosition(holdPoint.position);
        carriedRb.MoveRotation(holdPoint.rotation);
    }

    private void Update()
    {
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

            if (TryGetWorkStation(out WorkStation_SP station))
                station.RequestStopWork();
        }
    }

    private IEnumerator HoldWorkRoutine()
    {
        while (interactHeld)
        {
            if (!IsCarrying && TryGetWorkStation(out WorkStation_SP station))
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
            if (TryGetWorkStation(out WorkStation_SP station))
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

    public IPickupable GetCurrentItem()
    {
        return currentCarriable;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastDropTime < dropCooldown)
            return;

        ConstructObject_SP construct = other.GetComponentInParent<ConstructObject_SP>();
        if (construct != null)
        {
            SetCurrentTarget(construct.gameObject);
            Debug.Log("Construct target girildi: " + currentTarget.name);
            return;
        }

        WorkStation_SP station = other.GetComponentInParent<WorkStation_SP>();
        if (station != null)
        {
            SetCurrentTarget(station.gameObject);
            Debug.Log("WorkStation target girildi: " + currentTarget.name);
            return;
        }

        CarriableObject_SP carriable = other.GetComponentInParent<CarriableObject_SP>();
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
        GameObject exitedTarget = GetTargetFromCollider(other);

        if (exitedTarget != null && exitedTarget == currentTarget)
        {
            ClearCurrentTarget();
            Debug.Log("Target çýkýldý");
        }
    }

    private GameObject GetTargetFromCollider(Collider other)
    {
        ConstructObject_SP construct = other.GetComponentInParent<ConstructObject_SP>();
        if (construct != null) return construct.gameObject;

        WorkStation_SP station = other.GetComponentInParent<WorkStation_SP>();
        if (station != null) return station.gameObject;

        CarriableObject_SP carriable = other.GetComponentInParent<CarriableObject_SP>();
        if (carriable != null) return carriable.gameObject;

        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable is MonoBehaviour mb) return mb.gameObject;

        return null;
    }

    private void SetCurrentTarget(GameObject newTarget)
    {
        if (currentTarget == newTarget)
            return;

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

    private bool TryGetWorkStation(out WorkStation_SP station)
    {
        station = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);

        foreach (var hit in hits)
        {
            station = hit.GetComponentInParent<WorkStation_SP>();

            if (station != null)
                return true;
        }

        return false;
    }

    private void Drop()
    {
        if (!IsCarrying) return;

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

        Debug.Log("Item býrakýldý");
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

        Debug.Log("Item alýndý: " + obj.name);
    }

    private void PlaceToStation(WorkStation_SP station)
    {
        if (station == null) return;
        if (currentObj == null) return;

        CarriableObject_SP carriable = currentObj.GetComponent<CarriableObject_SP>();
        if (carriable == null) return;

        int recipeIndex = station.GetRecipeIndexForMaterial(carriable.Material);

        if (recipeIndex == -1)
        {
            Debug.Log("Bu materyal burada kullanýlamaz");
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

        station.PlaceItem(carriable, recipeIndex);

        Debug.Log("Item WorkStation'a yerleþtirildi: " + placedObj.name);
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

        ConstructObject_SP construct = target.GetComponentInParent<ConstructObject_SP>();

        if (construct != null)
        {
            if (!IsCarrying) return;
            if (currentObj == null) return;

            CarriableObject_SP carriedObj = currentObj.GetComponent<CarriableObject_SP>();
            if (carriedObj == null) return;

            bool built = false;

            try
            {
                built = construct.TryBuild(carriedObj);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Construct build sýrasýnda hata oluþtu: " + e.Message);
                return;
            }

            if (!built) return;

            GameObject destroyObj = currentObj;

            ReleaseHeldItemForStation();

            if (destroyObj != null)
                Destroy(destroyObj);

            Debug.Log("Build tamamlandý");
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

        ClearCurrentTarget();

        carriedRb = null;
        currentCarriable = null;
        currentObj = null;
    }

    private void OnDisable()
    {
        StopHoldWorkRoutine();
        RestoreHighlight();
    }
}