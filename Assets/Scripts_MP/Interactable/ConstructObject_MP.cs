using GameData;
using Interactions;
using ItemScript;
using Mirror;
using System;
using UnityEngine;

namespace ItemScript
{
    public class ConstructObject_MP : NetworkBehaviour, IConstructable, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] private ConstructProfile profile;

        [Header("Visuals")]
        [SerializeField] private GameObject interactionMesh;
        [SerializeField] private GameObject ghostMesh;
        [SerializeField] private GameObject builtMesh;

        // SyncVar: server'da true olunca tüm clientlara otomatik yayılır
        [SyncVar(hook = nameof(OnIsBuiltChanged))]
        private bool isBuilt = false;

        public Action<ConstructObject_MP> OnBuilt;

        public ConstructType ConstructType =>
            profile != null ? profile.constructType : ConstructType.Frame;

        public bool IsBuilt => isBuilt;

        private void Start()
        {
            UpdateVisuals(isBuilt);
        }

        public void Interact()
        {
            Debug.Log("ConstructObject Interact çağrıldı");
        }

        // PlayerInteract_MP bu metodu çağırır
        public bool TryBuild(CarriableObject_MP heldItem)
        {
            if (isBuilt) return false;
            if (heldItem == null) return false;
            if (profile == null) return false;
            if (heldItem.Material != profile.requiredMaterial) return false;

            // Server'a gönder
            NetworkIdentity itemIdentity = heldItem.GetComponent<NetworkIdentity>();
            if (itemIdentity == null)
            {
                Debug.LogError("CarriableObject_MP üzerinde NetworkIdentity yok!");
                return false;
            }

            CmdTryBuild(itemIdentity);
            return true; // lokal olarak "başarılı" say, server onaylayacak
        }

        [Command(requiresAuthority = false)]
        private void CmdTryBuild(NetworkIdentity itemIdentity)
        {
            if (isBuilt) return;
            if (itemIdentity == null) return;

            CarriableObject_MP item = itemIdentity.GetComponent<CarriableObject_MP>();
            if (item == null) return;
            if (profile == null) return;
            if (item.Material != profile.requiredMaterial) return;

            isBuilt = true; // SyncVar: tüm clientlara otomatik gider
            RpcOnBuilt();
        }

        [ClientRpc]
        private void RpcOnBuilt()
        {
            OnBuilt?.Invoke(this);
        }

        // SyncVar hook: isBuilt değişince tüm clientlarda visuals güncellenir
        private void OnIsBuiltChanged(bool oldVal, bool newVal)
        {
            UpdateVisuals(newVal);
        }

        private void UpdateVisuals(bool built)
        {
            if (interactionMesh != null) interactionMesh.SetActive(!built);
            if (ghostMesh != null) ghostMesh.SetActive(!built);
            if (builtMesh != null) builtMesh.SetActive(built);
        }
    }
}