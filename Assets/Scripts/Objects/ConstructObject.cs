using GameData;
using Interactions;
using Mirror;
using System;
using UnityEngine;

namespace ItemScript
{
    public class ConstructObject : NetworkBehaviour, IConstructable, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private ConstructProfile profile;

        [Header("Visuals (Assign in Inspector)")]
        [SerializeField] private GameObject interactionMesh;
        [SerializeField] private GameObject ghostMesh;
        [SerializeField] private GameObject builtMesh;

        [SyncVar(hook = nameof(OnBuiltStateChanged))]
        private bool isBuilt = false;

        public event Action<ConstructObject> OnBuilt;

        public ConstructType ConstructType =>
            profile != null ? profile.constructType : ConstructType.Frame;

        public bool IsBuilt => isBuilt;

        public override void OnStartClient()
        {
            base.OnStartClient();
            UpdateVisuals(isBuilt);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            UpdateVisuals(isBuilt);
        }

        private void OnBuiltStateChanged(bool _, bool current)
        {
            UpdateVisuals(current);
        }

        private void UpdateVisuals(bool built)
        {
            if (interactionMesh != null)
                interactionMesh.SetActive(!built);

            if (ghostMesh != null)
                ghostMesh.SetActive(!built);

            if (builtMesh != null)
                builtMesh.SetActive(built);
        }

        public void Interact()
        {
            Debug.Log("ConstructObject Interact çağrıldı");
        }

        public bool TryBuild(CarriableObject heldItem)
        {
            if (!isServer) return false;
            if (!CanBuildWith(heldItem)) return false;

            isBuilt = true;

            UpdateVisuals(isBuilt);

            OnBuilt?.Invoke(this);

            return true;
        }

        public bool CanBuildWith(CarriableObject heldItem)
        {
            if (isBuilt) return false;
            if (heldItem == null) return false;
            if (profile == null) return false;
            if (heldItem.Material != profile.requiredMaterial) return false;

            return true;
        }
    }
}