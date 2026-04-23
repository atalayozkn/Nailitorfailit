using GameData;
using Interactions;
using Mirror;
using PlayerScripts;
using System;
using UnityEngine;

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
        //private NetworkVariable<bool> isBuilt = new NetworkVariable<bool>(false); WITH THE MIRROR SYSTEM IT HAS BEEN CHANGED

        [SyncVar(hook = nameof(OnBuiltStateChanged))]
        private bool isBuilt = false;

        public Action<ConstructObject> OnBuilt;

        // Properties from Profile
        public ConstructType ConstructType => profile != null ? profile.constructType : ConstructType.Frame;
        public bool IsBuilt => isBuilt;

        /*public override void OnStartClient()
        {
            isBuilt.OnValueChanged += OnBuiltStateChanged;
            UpdateVisuals(isBuilt.Value);
        }*/

        /*public override void OnStopClient()
        {
            isBuilt.OnValueChanged -= OnBuiltStateChanged;
        }*/

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

        public void Interact()
        {
            if (isBuilt) return;

            CmdBuildServer();
        }


        [Command]//[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void CmdBuildServer()
        {
            isBuilt = true;
            // You could add logic here for "Partial Builds" (requiring 3 wood instead of 1)
        }
    }
}