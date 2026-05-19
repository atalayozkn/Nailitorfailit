using GameData;
using Interactions;
using System;
using UnityEngine;

namespace ItemScript
{
    public class ConstructObject_SP : MonoBehaviour, IConstructable, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private ConstructProfile profile;

        [Header("Visuals (Assign in Inspector)")]
        [SerializeField] private GameObject ghostMesh;
        [SerializeField] private GameObject builtMesh;

        private bool isBuilt = false;

        public Action<ConstructObject_SP> OnBuilt;

        public ConstructType ConstructType =>
            profile != null ? profile.constructType : ConstructType.Frame;

        public bool IsBuilt => isBuilt;

        private void Start()
        {
            UpdateVisuals(isBuilt);
        }

        private void UpdateVisuals(bool built)
        {
            if (ghostMesh)
                ghostMesh.SetActive(!built);

            if (builtMesh)
                builtMesh.SetActive(built);
        }

        public void Interact()
        {
            Debug.Log("ConstructObject Interact çaðrýldý");
        }

        public bool TryBuild(CarriableObject_SP heldItem)
        {
            if (isBuilt) return false;
            if (heldItem == null) return false;
            if (profile == null) return false;
            if (heldItem.Material != profile.requiredMaterial)
                return false;

            isBuilt = true;

            UpdateVisuals(isBuilt);

            OnBuilt?.Invoke(this);

            return true;
        }
    }
}