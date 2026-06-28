using GameData;
using Interactions;
using UnityEngine;

namespace ItemScript
{
    public class ConstructObject_SP : MonoBehaviour, IConstructable, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private ConstructProfile profile;

        [Header("Visuals (Assign in Inspector)")]
        [SerializeField] private GameObject interactionMesh;
        [SerializeField] private GameObject ghostMesh;
        [SerializeField] private GameObject builtMesh;

        private bool isBuilt = false;

        public ConstructType ConstructType =>
            profile != null ? profile.constructType : ConstructType.Frame;

        public bool IsBuilt => isBuilt;

        private void Start()
        {
            UpdateVisuals(isBuilt);
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
            Debug.Log("ConstructObject Interact çaðrýldý");
        }

        public bool TryBuild(CarriableObject_SP heldItem)
        {
            if (!CanBuildWith(heldItem))
                return false;

            isBuilt = true;

            UpdateVisuals(isBuilt);

            return true;
        }

        public bool CanBuildWith(CarriableObject_SP heldItem)
        {
            if (isBuilt) return false;
            if (heldItem == null) return false;
            if (profile == null) return false;
            if (heldItem.Material != profile.requiredMaterial) return false;

            return true;
        }
    }
}