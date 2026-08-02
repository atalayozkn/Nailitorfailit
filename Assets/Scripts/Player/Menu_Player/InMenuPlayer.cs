using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class InMenuPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform meshParent;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float drag = 5f;
    [SerializeField] private float rotationLerpSpeed = 10f;

    [Header("Detection")]
    [SerializeField] private LayerMask interactableLayerMask;
    [SerializeField] private LayerMask groundLayerMask;

    [Header("Inputs")]
    [SerializeField] private InputActionReference moveInputReference;
    [SerializeField] private InputActionReference interactInputReference;

    private Vector2 moveInput;
    private LevelSocket currentInteractable;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }
    private void OnEnable()
    {
        moveInputReference.action.Enable();
        interactInputReference.action.Enable();
    }
    private void OnDisable()
    {
        moveInputReference.action.Disable();
        interactInputReference.action.Disable();
    }
    private void Update()
    {
        moveInput = moveInputReference.action.ReadValue<Vector2>();

        if (currentInteractable == null) return;

        if (interactInputReference.action.WasPressedThisFrame())
        {
            currentInteractable.OnInteract();
        }
    }
    private void FixedUpdate()
    {
        rb.linearDamping = drag;

        Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y).normalized * moveSpeed;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = movement.x;
        velocity.z = movement.z;

        rb.linearVelocity = velocity;

        if (movement.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);

            meshParent.rotation = Quaternion.Slerp(
                meshParent.rotation,
                targetRotation,
                rotationLerpSpeed * Time.fixedDeltaTime
            );
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & interactableLayerMask) == 0)
            return;

        if (other.gameObject.TryGetComponent(out LevelSocket socket))
        {
            currentInteractable = socket;
            currentInteractable.SetActivity(true);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (currentInteractable == null)
            return;

        if (other.TryGetComponent(out LevelSocket socket) && socket == currentInteractable)
        {
            currentInteractable.SetActivity(false);
            currentInteractable = null;
        }
    }
    public bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f, groundLayerMask);
    }
}