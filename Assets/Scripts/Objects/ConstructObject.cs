using PlayerScripts;
using Unity.Netcode;
using UnityEngine;
using Interactions;
using GameData;

namespace ItemScript
{
    public class ConstructObject : NetworkBehaviour, IConstructable, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private ConstructProfile profile;

        [Header("Visuals (Assign in Inspector)")]
        [SerializeField] private GameObject ghostMesh;
        [SerializeField] private GameObject builtMesh;
        [SerializeField] private Collider blockingCollider; // The collider that stops players walking through

        // Network State
        private NetworkVariable<bool> isBuilt = new NetworkVariable<bool>(false);

        // Properties from Profile
        public ConstructType ConstructType => profile != null ? profile.constructType : ConstructType.Frame;
        public bool IsBuilt => isBuilt.Value;

        public override void OnNetworkSpawn()
        {
            isBuilt.OnValueChanged += OnBuiltStateChanged;
            UpdateVisuals(isBuilt.Value);
        }

        public override void OnNetworkDespawn()
        {
            isBuilt.OnValueChanged -= OnBuiltStateChanged;
        }

        private void OnBuiltStateChanged(bool prev, bool curr)
        {
            UpdateVisuals(curr);
        }

        private void UpdateVisuals(bool built)
        {
            if (ghostMesh) ghostMesh.SetActive(!built);
            if (builtMesh) builtMesh.SetActive(built);
            // if (blockingCollider) blockingCollider.enabled = built; // This is not needed because collider is on the builtmesh object
        }

        public bool Interact(IPickupable heldItem)
        {
            Debug.Log("Interacting with ConstructObject");
            if (isBuilt.Value) return false;
            if (heldItem == null) return false;
            if (profile == null) return false;

            // Check Material match
            if (heldItem.Material == profile.requiredMaterial)
            {
                BuildServerRpc();
                return true; // Return true to destroy the material in player's hand
            }

            return false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BuildServerRpc()
        {
            isBuilt.Value = true;
            // You could add logic here for "Partial Builds" (requiring 3 wood instead of 1)
        }
    }
}