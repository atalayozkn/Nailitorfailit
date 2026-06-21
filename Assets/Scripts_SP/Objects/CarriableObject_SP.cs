using PlayerScripts;
using UnityEngine;
using Interactions;

namespace ItemScript
{
    public class CarriableObject_SP : MonoBehaviour, IPickupable, IInteractable
    {
        [Header("Item Properties")]
        [SerializeField] private string itemType = "CarriableItem";

        [Header("Interaction Data")]
        [Tooltip("If this item is a resource material (e.g. Brick), set it here.")]
        [SerializeField] private MaterialType materialType = MaterialType.None;

        [SerializeField] private float itemWeight;

        [Tooltip("If this item is a tool (e.g. Hammer), set it here.")]
        [SerializeField] private Tools toolType = Tools.None;

        public string ItemType => itemType;
        public float Weight => itemWeight;
        public MaterialType Material => materialType;
        public Tools Tool => toolType;

        private Rigidbody rb;

        private float defaultLinearDamping;
        private float defaultAngularDamping;
        private bool defaultUseGravity;
        private RigidbodyConstraints defaultConstraints;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            if (rb != null)
            {
                defaultLinearDamping = rb.linearDamping;
                defaultAngularDamping = rb.angularDamping;
                defaultUseGravity = rb.useGravity;
                defaultConstraints = rb.constraints;
            }

            itemWeight = materialType.GetWeight();
        }

        public void Interact()
        {
            Debug.Log("Item alýndý");
        }

        public void InitializeObject(MaterialType typeOfMaterial)
        {
            materialType = typeOfMaterial;
            itemWeight = materialType.GetWeight();
        }

        public void OnPickUp()
        {
            if (rb == null) return;

            rb.useGravity = false;
            rb.linearDamping = 10f;
            rb.angularDamping = 10f;
            rb.freezeRotation = true;
            rb.WakeUp();
        }

        public void OnDrop()
        {
            if (rb == null) return;

            rb.useGravity = defaultUseGravity;
            rb.linearDamping = defaultLinearDamping;
            rb.angularDamping = defaultAngularDamping;
            rb.constraints = defaultConstraints;
            rb.WakeUp();
        }

        public Rigidbody GetRigidbody()
        {
            return rb;
        }

        public bool IsTool(Tools requiredTool)
        {
            return toolType == requiredTool;
        }

        public bool IsMaterial(MaterialType requiredMaterial)
        {
            return materialType == requiredMaterial;
        }
    }
}