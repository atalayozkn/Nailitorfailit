
using System.Collections;
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

        [Header("Life / Respawn")]
        [Tooltip("Visual object hidden while dead. Leave empty to hide nothing.")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private PlayerStateMachine playerStateMachine;

        /// <summary>
        /// Server-authoritative death state. Because it is a SyncVar, the hook runs on
        /// every client and shows/hides the visual for everyone.
        /// </summary>
        [SyncVar(hook = nameof(OnDeadChanged))]
        public bool isDead;

        private bool respawnRoutineRunning;

        private void Awake()
        {
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (interaction == null) interaction = GetComponent<PlayerInteractionHandler>();
            if (interactionNetwork == null) interactionNetwork = GetComponent<PlayerInteractionNetwork_MP>();
            if (stamina == null) stamina = GetComponent<PlayerStaminaHandler>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            if (crashHelper == null) crashHelper = GetComponent<PlayerCrashHelper>();
            if (trapRespawn == null) trapRespawn = GetComponent<PlayerTrapRespawn>();
            if (playerStateMachine == null) playerStateMachine = GetComponent<PlayerStateMachine>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            SetLocalOnlyActive(isOwned);
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

            SetLocalOnlyActive(true);
        }

        #region LIFE / RESPAWN

        /// <summary>
        /// Reports a death to the server. On the host, [Command] runs instantly; on a
        /// client it is sent to the server - both take the same path.
        ///
        /// RespawnManager calls this instead of SetActive(): calling SetActive on a
        /// networked object on the server detaches it from the other clients, and
        /// SetActive(true) does not re-spawn it, so that player never reappears.
        /// </summary>
        public void RequestDeath()
        {
            if (isDead || respawnRoutineRunning) return;
            if (!isOwned && !isServer) return;

            CmdRequestDeath();
        }

        [Command]
        private void CmdRequestDeath()
        {
            ServerDie();
        }

        [Server]
        private void ServerDie()
        {
            if (isDead || respawnRoutineRunning) return;

            respawnRoutineRunning = true;
            isDead = true;

            RespawnManager rm = FindAnyObjectByType<RespawnManager>();

            float delay = rm != null ? rm.SpawnDuration : 3f;
            Vector3 pos = (rm != null && rm.SpawnPoint != null) ? rm.SpawnPoint.position : transform.position;
            Quaternion rot = (rm != null && rm.SpawnPoint != null) ? rm.SpawnPoint.rotation : transform.rotation;

            StartCoroutine(ServerRespawnRoutine(delay, pos, rot));
        }

        private IEnumerator ServerRespawnRoutine(float delay, Vector3 pos, Quaternion rot)
        {
            yield return new WaitForSeconds(delay);

            transform.position = pos;
            transform.rotation = rot;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            isDead = false;
            respawnRoutineRunning = false;
        }

        private void OnDeadChanged(bool oldValue, bool newValue)
        {
            if (visualRoot != null) visualRoot.SetActive(!newValue);

            if (!newValue && isOwned && playerStateMachine != null)
            {
                playerStateMachine.enabled = false;
                playerStateMachine.enabled = true;
            }
        }

        #endregion

        /// <summary>
        /// Enables/disables the "LocalPlayerOnly" group on the player prefab root.
        /// It contains the Main Camera, InLevelCamera and the Overlay HUD canvas.
        /// The group sits under the root (a sibling of Player_Default), so it cannot be
        /// found with GetComponentInChildren - we look it up via transform.root.
        /// The dog's status canvas is world-space and deliberately outside this group,
        /// so remote players still see it.
        /// </summary>
        private void SetLocalOnlyActive(bool isLocal)
        {
            Transform localOnly = transform.root.Find("LocalPlayerOnly");

            if (localOnly == null)
            {
                Debug.LogWarning("[MP] 'LocalPlayerOnly' not found on the player prefab - " +
                                 "the camera and HUD stay enabled for remote players too.");
                return;
            }

            localOnly.gameObject.SetActive(isLocal);
        }
    }
}
