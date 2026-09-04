
using Mirror;
using Unity.Cinemachine;
using UnityEngine;

namespace PlayerScripts
{
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerInteractionHandler))]
    [RequireComponent(typeof(PlayerInteractionNetwork_MP))]
    [RequireComponent(typeof(PlayerStaminaHandler))]
    [RequireComponent(typeof(PlayerStateMachine))]
    public class PlayerNetwork_MP : NetworkBehaviour
    {
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerInteractionHandler interaction;
        [SerializeField] private PlayerInteractionNetwork_MP interactionNetwork;
        [SerializeField] private PlayerStaminaHandler stamina;
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private PlayerCrashHelper crashHelper;
        [SerializeField] private PlayerTrapRespawn trapRespawn;

        private void Awake()
        {
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (interaction == null) interaction = GetComponent<PlayerInteractionHandler>();
            if (interactionNetwork == null) interactionNetwork = GetComponent<PlayerInteractionNetwork_MP>();
            if (stamina == null) stamina = GetComponent<PlayerStaminaHandler>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            if (crashHelper == null) crashHelper = GetComponent<PlayerCrashHelper>();
            if (trapRespawn == null) trapRespawn = GetComponent<PlayerTrapRespawn>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            stateMachine.enabled = true;
            movement.enabled = true;
            interaction.enabled = false;
            interactionNetwork.enabled = true;
            stamina.enabled = true;
            if (crashHelper != null) crashHelper.enabled = true;
            if (trapRespawn != null) trapRespawn.enabled = true;

            ActivateLocalCameraRig();
        }

        private void ActivateLocalCameraRig()
        {
            Camera localCamera = GetComponentInChildren<Camera>(true);
            if (localCamera != null)
            {
                localCamera.gameObject.SetActive(true);
                localCamera.enabled = true;
            }

            AudioListener localListener = GetComponentInChildren<AudioListener>(true);
            if (localListener != null) localListener.enabled = true;

            CinemachineCamera localVirtualCamera = GetComponentInChildren<CinemachineCamera>(true);
            if (localVirtualCamera != null)
            {
                localVirtualCamera.gameObject.SetActive(true);
                localVirtualCamera.enabled = true;
            }
        }
    }
}
