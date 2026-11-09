using Interactions;
using UnityEngine;
using UnityEngine.Events;

namespace ItemScript
{
    public class CarriableObject_SP :MonoBehaviour, IInteractable, ILookAtInfo, ISpawnable
    {
        [Header("References")]
        [SerializeField] private InteractableType interactableType;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Collider[] cols;
        [SerializeField] private MeshRenderer objectRenderer;
        public bool isRawMaterial = false;
        public CarriableType carriableType;

        [Header("Settings")]
        [SerializeField] private float dropForce = 1f;
        [SerializeField] private float throwMultiplier = 0.1f;
        [Tooltip("Upward speed (m/s) added to a throw so it arcs over low fences. Mass independent, so it is the same for every item.")]
        [SerializeField] private float throwUpwardSpeed = 5f;
        [SerializeField] private float objectDiscardDelay = 3.0f;

        [Header("Events")]
        [SerializeField] private UnityEvent onHoverOnEvent;
        [SerializeField] private UnityEvent onHoverOffEvent;
        [SerializeField] private UnityEvent onInteractEvent;
        [SerializeField] private UnityEvent onConsumeEvent;
        public InteractableType InteractableType => interactableType;

        private ObjectSpawner spawnerObject;
        private PlayerInteractionHandler interactionHandler;
        private bool isOccupied = false;
        private bool isConsumed;
        private Transform attachTransform;
        private void Awake()
        {
            interactionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
        }

        #region INTERACTABLE

        public bool OnInteract()
        {
            if (isConsumed)
            {
                return false;
            }

            SnapToCarryTransform();
            onInteractEvent?.Invoke();
            return true;
        }
        public LookAtInfo GetLookAtInfo(CarriableObject_SP carried)
        {
            if (isConsumed || isOccupied) return default;
            // With a free inventory slot the held item is stowed, so only a full inventory swaps.
            bool wouldSwap = interactionHandler != null && interactionHandler.PickupWouldSwap();
            return LookAtInfo.Ready(LookAtText.ItemName(this), wouldSwap ? "Swap" : "Carry");
        }
        public void OnHoverOn()
        {
            onHoverOnEvent?.Invoke();
        }
        public void OnHoverOff()
        {
            onHoverOffEvent?.Invoke();
        }

        #endregion

        #region SPAWNABLE

        public void OnSpawn(GameObject spawner)
        {
            spawnerObject = spawner.GetComponent<ObjectSpawner>();
        }

        #endregion

        #region PLAYER INTERACTION
        private void SnapToCarryTransform()
        {
            SetCollusion(false);
            rb.isKinematic = true;
            rb.Sleep();

            Transform carryTransform = interactionHandler.GetCarryTransform();

            transform.SetParent(carryTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            interactionHandler.RegisterCarriedObject(this);
        }
        public void SnapToRightHand()
        {
            SetCollusion(false);
            rb.isKinematic = true;
            rb.Sleep();

            Transform rightHandTransform = interactionHandler.GetRightHandTransform();

            transform.SetParent(rightHandTransform);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            interactionHandler.RegisterCarriedObject(this);
        }
        public void OnDrop(bool shouldThrow = false)
        {
            if (isConsumed) return;
            transform.SetParent(null);
            rb.isKinematic = false;
            SetCollusion(true);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();

            if (shouldThrow)
            {
                float angle = Random.Range(0f, 30f);
                float rotation = Random.Range(0f, 360f);
                Quaternion spread = Quaternion.Euler(angle, rotation, 0f);
                Vector3 direction = spread * Vector3.up;
                rb.AddForce(direction * dropForce, ForceMode.Impulse);
            }

            interactionHandler.ReleaseCarried(this);
        }
        public void OnThrow(float force)
        {
            if (isConsumed) return;
            transform.SetParent(null);
            rb.isKinematic = false;
            SetCollusion(true);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();

            float adjustedForce = force * throwMultiplier;
            Vector3 direction = interactionHandler.transform.forward;
            rb.AddForce(direction * adjustedForce, ForceMode.Impulse);
            rb.AddForce(Vector3.up * throwUpwardSpeed, ForceMode.VelocityChange);
            interactionHandler.ReleaseCarried(this);
        }
        public void OnConsume()
        {
            if (isConsumed) return;
            isConsumed = true;

            spawnerObject?.ReduceCounter();
            onConsumeEvent?.Invoke();
            interactionHandler.ReleaseCarried(this);

            HideConsumed();
            Destroy(gameObject, objectDiscardDelay);
        }
        // Gone from view at once; the object itself lives on for the discard delay so its sounds can finish.
        private void HideConsumed()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer)) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var canvas in GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
        #endregion

        #region DOG INTERACTION
        public void PickUpByDog(Transform target)
        {
            //Phyics & Occupation
            isOccupied = true;
            SetCollusion(false);
            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();

            //Clear from player if Player is holding this.
            if (interactionHandler.GetCurrentCarriable() == this)
            {
                interactionHandler.ReleaseCarried(this);
            }

            //Attach to new target.
            attachTransform = target;
            transform.SetParent(attachTransform);
            Invoke(nameof(AttachToTransform), 0.1f);
        }
        private void AttachToTransform()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        public void DropByDog(Transform target)
        {
            //Cancel pending attach, otherwise it snaps back if dropped within 0.1s of pickup.
            CancelInvoke(nameof(AttachToTransform));
            isOccupied = false;
            attachTransform = null;

            transform.SetParent(target);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.SetParent(null);

            rb.detectCollisions = true;
            rb.useGravity = true;
            SetCollusion(true);
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
        public void DestroyedByDog()
        {
            spawnerObject?.ReduceCounter();
            onConsumeEvent?.Invoke();
            isOccupied = false;
            HideConsumed();
            Destroy(gameObject, objectDiscardDelay);
        }
        #endregion

        #region UTILITIES
        private void SetCollusion(bool condition)
        {
            foreach (var col in cols)
            {
                col.enabled = condition;
            }
        }
        public void SetOccupied()
        {
            isOccupied = true;
        }    
        public bool IsOccupied()
        {
            return isOccupied;
        }
        public void SetVisuals(bool condition)
        {
            if (condition == objectRenderer.enabled) return;
            objectRenderer.enabled = condition;
        }

        // Inventory: a stowed item rides along with the player but is not drawn.
        // Renderers are toggled instead of SetActive, because OnEnable resets state on some items (extinguisher fuel).
        public bool IsStowed { get; private set; }
        private readonly System.Collections.Generic.List<Renderer> stowedRenderers = new();
        private readonly System.Collections.Generic.List<Canvas> stowedCanvases = new();
        private const string HoverGlowName = "__HoverGlow";

        public void SetStowed(bool stowed)
        {
            if (stowed == IsStowed) return;
            IsStowed = stowed;

            if (stowed)
            {
                stowedRenderers.Clear();
                stowedCanvases.Clear();
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled || r.name == HoverGlowName) continue;
                    stowedRenderers.Add(r);
                    r.enabled = false;
                }
                foreach (var canvas in GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.enabled) continue;
                    stowedCanvases.Add(canvas);
                    canvas.enabled = false;
                }
                return;
            }

            foreach (var r in stowedRenderers) if (r != null) r.enabled = true;
            foreach (var canvas in stowedCanvases) if (canvas != null) canvas.enabled = true;
            stowedRenderers.Clear();
            stowedCanvases.Clear();
        }
        // The meshes that make up the item's normal look, for the inventory icon. Works while stowed too.
        public void GetIconRenderers(System.Collections.Generic.List<Renderer> result)
        {
            result.Clear();
            if (IsStowed)
            {
                foreach (var r in stowedRenderers)
                    if (r is MeshRenderer || r is SkinnedMeshRenderer) result.Add(r);
                return;
            }
            foreach (var r in GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r.name == HoverGlowName) continue;
                if (r is MeshRenderer || r is SkinnedMeshRenderer) result.Add(r);
            }
        }
    }

        #endregion
}
