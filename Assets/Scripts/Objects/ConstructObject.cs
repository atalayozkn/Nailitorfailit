using PlayerScripts;
using Unity.Netcode;
using UnityEngine;
using Interactions;
using GameData;
using System;

namespace ItemScript
{
    public class ConstructObject : NetworkBehaviour, IConstructable, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private ConstructProfile profile;

        [Header("Visuals (Assign in Inspector)")]
        [SerializeField] private GameObject ghostMesh;
        [SerializeField] private GameObject builtMesh;
        [SerializeField] private Collider blockingCollider;

        // 🔥 BUILD EVENT
        public Action<ConstructObject> OnBuilt;

        // Network State
        private NetworkVariable<bool> isBuilt = new NetworkVariable<bool>(false);

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

            // 🔥 SINGLEPLAYER için build event
            if (curr)
                OnBuilt?.Invoke(this);
        }

        private void UpdateVisuals(bool built)
        {
            if (ghostMesh) ghostMesh.SetActive(!built);
            if (builtMesh) builtMesh.SetActive(built);
        }

        public bool Interact(IPickupable heldItem)
        {
            if (isBuilt.Value) return false;
            if (heldItem == null) return false;
            if (profile == null) return false;

            if (heldItem.Material == profile.requiredMaterial)
            {
                BuildServerRpc();
                return true;
            }

            return false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void BuildServerRpc()
        {
            isBuilt.Value = true;
        }
    }
}