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

    [Header("DEBUG")]
    [SerializeField] private GameObject currentTarget;

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

        // BASILI TUTMA -> sadece WorkStation work
        if (interactAction.action.IsPressed())
        {
            if (!IsCarrying && TryGetWorkStation(out WorkStation_SP station))
            {
                station.RequestHoldWork();
            }
        }

        // TEK BASMA
        if (interactAction.action.WasPressedThisFrame())
        {
            // 1) Elde item varsa önce WorkStation kontrol et
            if (IsCarrying)
            {
                if (TryGetWorkStation(out WorkStation_SP station))
                {
                    PlaceToStation(station);
                    return;
                }

                // 2) WorkStation deðilse ama bir interact target varsa
                if (currentTarget != null)
                {
                    InteractWithTarget(currentTarget);
                    return;
                }

                // 3) Hiçbir target yoksa býrak
                Drop();
                return;
            }

            // 4) Elde item yoksa normal interact
            if (currentTarget != null)
            {
                InteractWithTarget(currentTarget);
            }
        }
    }

    public IPickupable GetCurrentItem()
    {
        return currentCarriable;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastDropTime < dropCooldown)
            return;

        // 1) ConstructObject_SP kontrolü
        ConstructObject_SP construct = other.GetComponentInParent<ConstructObject_SP>();
        if (construct != null)
        {
            currentTarget = construct.gameObject;
            Debug.Log("Construct target girildi: " + currentTarget.name);
            return;
        }

        // 2) WorkStation_SP kontrolü
        WorkStation_SP station = other.GetComponentInParent<WorkStation_SP>();
        if (station != null)
        {
            currentTarget = station.gameObject;
            Debug.Log("WorkStation target girildi: " + currentTarget.name);
            return;
        }

        // 3) CarriableObject_SP kontrolü
        CarriableObject_SP carriable = other.GetComponentInParent<CarriableObject_SP>();
        if (carriable != null)
        {
            currentTarget = carriable.gameObject;
            Debug.Log("Pickup target girildi: " + currentTarget.name);
            return;
        }

        // 4) Normal interactable kontrolü
        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable != null)
        {
            MonoBehaviour mb = interactable as MonoBehaviour;

            if (mb != null)
            {
                currentTarget = mb.gameObject;
                Debug.Log("Interactable target girildi: " + currentTarget.name);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        GameObject exitedTarget = null;

        ConstructObject_SP construct = other.GetComponentInParent<ConstructObject_SP>();
        if (construct != null)
            exitedTarget = construct.gameObject;

        WorkStation_SP station = other.GetComponentInParent<WorkStation_SP>();
        if (station != null)
            exitedTarget = station.gameObject;

        CarriableObject_SP carriable = other.GetComponentInParent<CarriableObject_SP>();
        if (carriable != null)
            exitedTarget = carriable.gameObject;

        IInteractable interactable = other.GetComponentInParent<IInteractable>();
        if (interactable != null && interactable is MonoBehaviour mb)
            exitedTarget = mb.gameObject;

        if (exitedTarget != null && exitedTarget == currentTarget)
        {
            currentTarget = null;
            Debug.Log("Target çýkýldý");
        }
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
            {
                Physics.IgnoreCollision(playerCollider, itemCollider, false);
            }
        }

        currentTarget = null;
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

        currentTarget = null;
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
        {
            Physics.IgnoreCollision(playerCollider, itemCollider, true);
        }

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

        ReleaseHeldItemForStation();

        station.PlaceItem(carriable, recipeIndex);

        Debug.Log("Item WorkStation'a yerleþtirildi");
    }

    private void InteractWithTarget(GameObject target)
    {
        if (target == null) return;

        // PICKUP
        if (target.CompareTag("Pickup"))
        {
            if (IsCarrying) return;

            Pickup(target);
            return;
        }

        // CONSTRUCT
        if (target.TryGetComponent<ConstructObject_SP>(out var construct))
        {
            if (!IsCarrying) return;

            CarriableObject_SP carriedObj = currentObj.GetComponent<CarriableObject_SP>();
            if (carriedObj == null) return;

            bool built = construct.TryBuild(carriedObj);
            if (!built) return;

            GameObject destroyObj = currentObj;

            ReleaseHeldItemForStation();

            Destroy(destroyObj);

            Debug.Log("Build tamamlandý");
            return;
        }

        if (construct != null)
        {
            if (!IsCarrying) return;

            CarriableObject_SP carriedObj = currentObj.GetComponent<CarriableObject_SP>();
            if (carriedObj == null) return;

            bool built = construct.TryBuild(carriedObj);
            if (!built) return;

            GameObject destroyObj = currentObj;

            ReleaseHeldItemForStation();

            Destroy(destroyObj);

            Debug.Log("Build tamamlandý");
            return;
        }

        // NORMAL INTERACT
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
            {
                Physics.IgnoreCollision(playerCollider, itemCollider, false);
            }
        }

        currentTarget = null;
        carriedRb = null;
        currentCarriable = null;
        currentObj = null;
    }
}
