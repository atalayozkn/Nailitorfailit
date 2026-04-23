using PlayerScripts;
using Mirror;
using UnityEngine;
using Interactions;

namespace ItemScript
{
    public class CarriableObject : NetworkBehaviour, IPickupable, IInteractable
    {
        [Header("Item Properties")]
        [SerializeField] private string itemType = "CarriableItem";

        [Header("Interaction Data")]
        [Tooltip("If this item is a resource material (e.g. Brick), set it here.")]
        [SerializeField]
        /*private NetworkVariable<MaterialType> netMaterialType = new NetworkVariable<MaterialType>(
            MaterialType.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );*/
        private MaterialType netMaterialType = MaterialType.None;
        [SerializeField] private float itemWeight;


        [Tooltip("If this item is a tool (e.g. Hammer), set it here.")]
        [SerializeField] private Tools toolType = Tools.None;
        
        // Interface Implementation
        public string ItemType => itemType;
        public float Weight => itemWeight;
        //public MaterialType Material => netMaterialType.Value; OLD COOP SYSTEM 
        public MaterialType Material => netMaterialType;
        public Tools Tool => toolType;

        private Rigidbody rb;

        // Default Physics State
        private float defaultLinearDamping;
        private float defaultAngularDamping;
        private bool defaultUseGravity;
        private RigidbodyConstraints defaultConstraints;


        public void Interact()
        {
            Debug.Log("Item alýndý");
            // pickup logic buraya
        }

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
        }

        public override void OnStartClient()
        {
            //base.OnNetworkSpawn();
            base.OnStartClient();
            float itemWeight = netMaterialType.GetWeight();
        }

        public void InitializeObject(MaterialType typeOfMaterial)
        {
            if (isServer)
            {
                netMaterialType = typeOfMaterial;
            }
            else
            {
                Debug.LogWarning("Only the Server can initialize the material type!");
            }
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
        }

        public Rigidbody GetRigidbody()
        {
            return rb;
        }

        // Helper to check if this object acts as a specific tool
        public bool IsTool(Tools requiredTool)
        {
            return toolType == requiredTool;
        }

        // Helper to check if this object is a specific material
        public bool IsMaterial(MaterialType requiredMaterial)
        {
            return netMaterialType == requiredMaterial;
        }
    }
}