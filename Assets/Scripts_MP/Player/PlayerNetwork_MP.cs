using System.Collections;
using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private float respawnDelay = 5f;

        private InputActionReference moveAction;
        private InputActionReference jumpAction;
        private InputActionReference sprintAction;

        private Animator netAnimator;
        private int lastSentAnimHash;

        [SyncVar(hook = nameof(OnNetAnimStateChanged))]
        private int netAnimStateHash;

        private static readonly int emptyStateHash = Animator.StringToHash("Empty");

        [SyncVar(hook = nameof(OnDeadChanged))]
        public bool isDead;

        private bool respawnRoutineRunning;

        private Rigidbody body;

        private DogStateMachine dogAI;
        private bool dogAiWaitingForNavMesh;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (interaction == null) interaction = GetComponent<PlayerInteractionHandler>();
            if (interactionNetwork == null) interactionNetwork = GetComponent<PlayerInteractionNetwork_MP>();
            if (stamina == null) stamina = GetComponent<PlayerStaminaHandler>();
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            if (crashHelper == null) crashHelper = GetComponent<PlayerCrashHelper>();
            if (trapRespawn == null) trapRespawn = GetComponent<PlayerTrapRespawn>();
            if (playerStateMachine == null) playerStateMachine = GetComponent<PlayerStateMachine>();

            if (movement != null)
            {
                moveAction = movement.move;
                jumpAction = movement.jump;
                sprintAction = movement.sprint;
            }

            netAnimator = stateMachine != null ? stateMachine.animator : null;
            if (netAnimator == null)
            {
                int carryIdleHash = Animator.StringToHash("CarryIdle");
                foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.runtimeAnimatorController != null && candidate.HasState(0, carryIdleHash))
                    {
                        netAnimator = candidate;
                        break;
                    }
                }
            }
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            if (body != null) body.isKinematic = false;
            ApplyMovementInput(true);
        }

        public override void OnStopAuthority()
        {
            base.OnStopAuthority();
            ApplyMovementInput(false);
        }

        private void ApplyMovementInput(bool enabled)
        {
            if (movement == null) return;

            movement.move = enabled ? moveAction : null;
            movement.jump = enabled ? jumpAction : null;
            movement.sprint = enabled ? sprintAction : null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            SetLocalOnlyActive(isOwned);

            ApplyMovementInput(isOwned);

            if (interaction != null) interaction.enabled = false;

            dogAI = GetComponentInChildren<DogStateMachine>(true);
            if (dogAI != null)
            {
                dogAI.enabled = isOwned && MpLevelInstaller.NavMeshReady;
                dogAiWaitingForNavMesh = isOwned && !MpLevelInstaller.NavMeshReady;
            }

            PauseMenu pauseMenu = transform.root.GetComponent<PauseMenu>();
            if (pauseMenu != null)
            {
                pauseMenu.enabled = isOwned;
            }

            EmoteListener emoteListener = GetComponent<EmoteListener>();
            if (emoteListener != null) emoteListener.enabled = isOwned;

            ApplyRemotePhysics();
        }

        private void ApplyRemotePhysics()
        {
            if (body == null) return;
            if (isOwned) return;
            if (!body.isKinematic) body.isKinematic = true;
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

            PauseMenu pauseMenu = transform.root.GetComponent<PauseMenu>();
            if (pauseMenu != null) MpSharedCompat.BindPauseStateMachine(pauseMenu, stateMachine);

            MpSharedCompat.RefreshLocalCurrencyText(CurrencyManager.Instance);

            WireLocalCamera();

            if (MpSharedCompat.HasAuthoredTimeHud || MpSharedCompat.HasAuthoredMoneyHud)
            {
                GameHUD_MP hud = transform.root.GetComponentInChildren<GameHUD_MP>(true);
                if (hud != null)
                {
                    hud.HideAuthoredDuplicates(
                        MpSharedCompat.HasAuthoredTimeHud,
                        MpSharedCompat.HasAuthoredMoneyHud);
                }
            }

            if (movement != null) movement.SetActivity(true);
            if (interaction != null) interaction.SetActivity(true);

            Debug.Log($"[MP] Local player ready. name={name} netId={netId} " +
                      $"isServer={isServer} isClient={isClient} pos={transform.position} " +
                      $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");

            if (stateMachine != null && stateMachine.animator != null)
            {
                Animator anim = stateMachine.animator;
                if (!anim.HasState(0, Animator.StringToHash("Idle")))
                {
                    Debug.LogWarning($"[MP] '{anim.name}' Animator'inda 'Idle' state'i yok (controller=" +
                                     $"{(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")}). " +
                                     "Controller, paylasilan Player state'lerinin kullandigi isimleri " +
                                     "(Idle/Run/Walk/CarryIdle/Jump) icermeli; Player_Package_Default_MP prefabinda " +
                                     "Player01_Male_Controller kullanilir.");
                }
                else
                {
                    Debug.Log($"[MP] Animator hazir. controller={anim.runtimeAnimatorController.name} " +
                              "(Idle/Run/Walk/CarryIdle/Jump state'leri bulundu)");
                }
            }
        }

        #region ANIMATION SYNC

        private void Update()
        {
            ApplyRemotePhysics();

            if (dogAiWaitingForNavMesh && MpLevelInstaller.NavMeshReady)
            {
                dogAiWaitingForNavMesh = false;
                if (dogAI != null) dogAI.enabled = true;
            }

            if (!isOwned || netAnimator == null || netAnimator.runtimeAnimatorController == null) return;

            int hash = netAnimator.IsInTransition(0)
                ? netAnimator.GetNextAnimatorStateInfo(0).shortNameHash
                : netAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;

            if (hash == emptyStateHash || hash == lastSentAnimHash) return;

            lastSentAnimHash = hash;

            if (isServer) netAnimStateHash = hash;
            else CmdSetAnimState(hash);
        }

        [Command]
        private void CmdSetAnimState(int hash)
        {
            netAnimStateHash = hash;
        }

        private void OnNetAnimStateChanged(int oldHash, int newHash)
        {
            if (isOwned || netAnimator == null || netAnimator.runtimeAnimatorController == null) return;
            if (newHash == 0) return;

            if (netAnimator.HasState(0, newHash))
            {
                netAnimator.CrossFadeInFixedTime(newHash, 0.1f);
            }
            else
            {
                Debug.LogWarning($"[MP] Uzak oyuncuda '{newHash}' animasyon state'i yok " +
                                 $"(controller={netAnimator.runtimeAnimatorController.name}).");
            }
        }

        #endregion

        #region LIFE / RESPAWN

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

            Transform spawnRoot = null;
            GameObject spawnMarker = GameObject.Find(LobbyNetworkManager.SpawnMarkerName);
            if (spawnMarker != null) spawnRoot = spawnMarker.transform;

            Vector3 pos = spawnRoot != null ? spawnRoot.position : transform.position;
            Quaternion rot = spawnRoot != null ? spawnRoot.rotation : transform.rotation;

            StartCoroutine(ServerRespawnRoutine(respawnDelay, pos, rot));
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
            if (playerStateMachine == null)
            {
                if (visualRoot != null) visualRoot.SetActive(!newValue);
                return;
            }

            if (newValue)
            {
                playerStateMachine.SetDeathReason(DeathReason.Car);
                playerStateMachine.ChangeToDeadState();
                return;
            }

            playerStateMachine.SetDead(false);
            playerStateMachine.ForceUpdateIdle();

            if (playerStateMachine.playerRenderer != null) playerStateMachine.playerRenderer.enabled = true;
            if (visualRoot != null) visualRoot.SetActive(true);
            if (isOwned && movement != null) movement.enabled = true;
        }

        #endregion

        private void WireLocalCamera()
        {
            Transform package = transform.root;
            CinemachineCamera[] cameras = package.GetComponentsInChildren<CinemachineCamera>(true);

            if (cameras.Length == 0)
            {
                InteractionDebug.Log($"[MP-CAM] '{package.name}': no CinemachineCamera under the player - camera follow unchanged.");
                return;
            }

            Transform follow = transform;
            CinemachineCamera live = null;

            foreach (CinemachineCamera camera in cameras)
            {
                if (camera.name.Contains("InLevel")) live = camera;
            }

            if (live == null) live = cameras[cameras.Length - 1];

            CameraTarget targets = live.Target;
            targets.TrackingTarget = follow;
            live.Target = targets;

            foreach (CinemachineCamera camera in cameras)
            {
                if (camera.enabled != (camera == live)) camera.enabled = camera == live;
            }

            int disabledCameras = 0;

            foreach (UnityEngine.Camera playerCamera in package.GetComponentsInChildren<UnityEngine.Camera>(true))
            {
                bool keep = playerCamera.gameObject.name == "Main Camera" || playerCamera.name == "Main Camera";

                if (playerCamera.enabled == keep) continue;

                playerCamera.enabled = keep;
                if (!keep) disabledCameras++;
            }

            foreach (CinemachineBrain brain in package.GetComponentsInChildren<CinemachineBrain>(true))
                brain.enabled = true;

            InteractionDebug.Log($"[MP-CAM] '{package.name}': live vcam '{live.name}' follows '{follow.name}' " +
                                 $"(vcams={cameras.Length}, extraCamerasOff={disabledCameras}).");
        }

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
