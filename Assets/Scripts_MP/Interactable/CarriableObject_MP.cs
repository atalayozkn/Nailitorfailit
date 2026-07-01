using Mirror;
using UnityEngine;
using Interactions;
using PlayerScripts;

namespace ItemScript
{
    public class CarriableObject_MP : NetworkBehaviour, IPickupable, IInteractable
    {
        [Header("Item Properties")]
        [SerializeField] private string itemType = "CarriableItem";

        [Header("Interaction Data")]
        [SerializeField] private MaterialType materialType = MaterialType.None;
        [SerializeField] private float itemWeight;
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

        public void Interact() => Debug.Log("Item alindi");

        public void InitializeObject(MaterialType typeOfMaterial)
        {
            materialType = typeOfMaterial;
            itemWeight = materialType.GetWeight();
        }

        // --- IPickupable ---

        public void OnPickUp()
        {
            ApplyPickupPhysics(); // lokal anlık tepki
            if (isServer) RpcSyncPickup();
            else CmdRequestPickup();
        }

        public void OnDrop()
        {
            ApplyDropPhysics();
            if (isServer) RpcSyncDrop();
            else CmdRequestDrop();
        }

        // --- Command zinciri: client → server → tüm clientlar ---

        // requiresAuthority = false: player bu item'ın sahibi olmasa da çağırabilir
        [Command(requiresAuthority = false)]
        private void CmdRequestPickup() => RpcSyncPickup();

        [Command(requiresAuthority = false)]
        private void CmdRequestDrop() => RpcSyncDrop();

        [ClientRpc]
        private void RpcSyncPickup() => ApplyPickupPhysics();

        [ClientRpc]
        private void RpcSyncDrop() => ApplyDropPhysics();

        // --- Fizik değişiklikleri ---

        private void ApplyPickupPhysics()
        {
            if (rb == null) return;
            rb.useGravity = false;
            rb.linearDamping = 10f;
            rb.angularDamping = 10f;
            rb.freezeRotation = true;
            rb.WakeUp();
        }

        private void ApplyDropPhysics()
        {
            if (rb == null) return;
            rb.useGravity = defaultUseGravity;
            rb.linearDamping = defaultLinearDamping;
            rb.angularDamping = defaultAngularDamping;
            rb.constraints = defaultConstraints;
            rb.WakeUp();
        }

        // --- Yardımcılar ---

        public Rigidbody GetRigidbody() => rb;
        public bool IsTool(Tools requiredTool) => toolType == requiredTool;
        public bool IsMaterial(MaterialType requiredMaterial) => materialType == requiredMaterial;
    }
}