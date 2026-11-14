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

        // Declared before isDead so it is applied first when both change in the same update.
        [SyncVar] private int netDeathReason = (int)DeathReason.Car;

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

        // A player leaving while carrying something would destroy the carried (networked) object together
        // with its hand transform. Unparent it first so it stays in the world for everyone.
        public override void OnStopClient()
        {
            base.OnStopClient();

            foreach (CarriableObject_MP carried in GetComponentsInChildren<CarriableObject_MP>(true))
            {
                carried.transform.SetParent(null, true);

                if (carried.TryGetComponent(out Rigidbody carriedBody)) carriedBody.isKinematic = false;
                foreach (Collider carriedCollider in carried.GetComponents<Collider>()) carriedCollider.enabled = true;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            SetLocalOnlyActive(isOwned);

            ApplyMovementInput(isOwned);

            if (interaction != null) interaction.enabled = false;

            // This component lives on Player_Default; the dog is under the sibling DogPackage, so search
            // from the package root (same as DogSync_MP). A child search never found it, which left the
            // remote player's dog AI running against a disabled agent and skipped the NavMesh wait.
            dogAI = transform.root.GetComponentInChildren<DogStateMachine>(true);
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

            if (isOwned) UpdateEmoteThrow();

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

        // The SP state machine also runs on remote copies and crossfades on its own (e.g. RegisterCarriedObject
        // -> ForceUpdateIdle shows CarryIdle while the owner is walking). The owner's state is the truth:
        // re-apply it whenever the remote animator drifted. Skipped while the animator is off (ragdoll / stun).
        private static readonly int idleStateHash = Animator.StringToHash("Idle");
        private static readonly int carryIdleStateHash = Animator.StringToHash("CarryIdle");
        private float emptySince = -1f;

        private void LateUpdate()
        {
            if (netAnimator == null || !netAnimator.enabled || !netAnimator.isActiveAndEnabled) return;
            if (netAnimator.runtimeAnimatorController == null || isDead) return;

            int shown = netAnimator.IsInTransition(0)
                ? netAnimator.GetNextAnimatorStateInfo(0).shortNameHash
                : netAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;

            // T-pose guard (owner and remote): the controller's default "Empty" state has no motion. If the
            // first Idle crossfade was missed (animator not ready / rebound on spawn) the player stood in a
            // T-pose until it moved. Leave it after a short grace period.
            if (shown == emptyStateHash)
            {
                if (emptySince < 0f) emptySince = Time.time;
                if (Time.time - emptySince < 0.3f) return;
                emptySince = -1f;

                int target = !isOwned && netAnimStateHash != 0 && netAnimStateHash != emptyStateHash
                    ? netAnimStateHash
                    : (interaction != null && interaction.IsCarrying() ? carryIdleStateHash : idleStateHash);

                if (netAnimator.HasState(0, target)) netAnimator.CrossFadeInFixedTime(target, 0.1f);
                return;
            }

            emptySince = -1f;

            if (isOwned || netAnimStateHash == 0) return;
            if (shown == netAnimStateHash || !netAnimator.HasState(0, netAnimStateHash)) return;

            netAnimator.CrossFadeInFixedTime(netAnimStateHash, 0.1f);
        }

        #endregion

        #region OWNER-SIDE SPAWNS (dog Aid)

        private float nextOwnerSpawnAllowed;

        // The dog AI runs on its owner's machine; items it creates there must be spawned by the server.
        public void RequestSpawnCarriable(string prefabName, Vector3 position, Quaternion rotation)
        {
            if (isOwned) CmdSpawnCarriable(prefabName, position, rotation);
        }

        [Command]
        private void CmdSpawnCarriable(string prefabName, Vector3 position, Quaternion rotation)
        {
            // Rate limit + only registered networked carriables, so a client can't spawn arbitrary things.
            if (Time.time < nextOwnerSpawnAllowed) return;
            nextOwnerSpawnAllowed = Time.time + 2f;

            if ((position - transform.position).sqrMagnitude > 30f * 30f) return; // must be near this player

            GameObject prefab = null;
            foreach (GameObject candidate in NetworkManager.singleton.spawnPrefabs)
            {
                if (candidate != null && candidate.name == prefabName && candidate.GetComponent<CarriableObject_MP>() != null)
                {
                    prefab = candidate;
                    break;
                }
            }

            if (prefab == null)
            {
                InteractionDebug.Log($"[MP] owner spawn refused: '{prefabName}' is not a networked carriable.");
                return;
            }

            NetworkServer.Spawn(Instantiate(prefab, position, rotation));
        }

        #endregion

        #region EMOTE THROW

        // PlayerEmoteState (index 0 = Throw) throws the held item with the SP OnThrow, which is local only and
        // clears whichever handler FindFirstObjectByType found. For networked items the owner releases it
        // just before the SP action time and the server throws it for everyone instead.
        private const float EmoteThrowActionTime = 1.05f;
        private float emoteThrowAt = -1f;

        private void UpdateEmoteThrow()
        {
            if (playerStateMachine == null) return;

            bool throwing = playerStateMachine.currentPlayerState == PlayerStates.Emote &&
                            playerStateMachine.activeEmoteIndex == 0;

            if (!throwing)
            {
                emoteThrowAt = -1f;
                return;
            }

            PlayerInteractionHandler handler = interaction != null ? interaction : GetComponent<PlayerInteractionHandler>();
            ItemScript.CarriableObject_SP held = handler != null ? handler.GetCurrentCarriable() : null;
            if (held == null || !held.TryGetComponent(out CarriableObject_MP _)) return;

            if (emoteThrowAt < 0f)
            {
                emoteThrowAt = Time.time + EmoteThrowActionTime;
                return;
            }

            if (Time.time < emoteThrowAt) return;

            emoteThrowAt = float.MaxValue; // once per emote
            handler.ClearCarriedObject();  // the SP state now finds nothing to throw
            CmdThrowCarried(transform.forward);
        }

        [Command]
        private void CmdThrowCarried(Vector3 direction)
        {
            foreach (CarriableObject_MP item in FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None))
            {
                if (item.Carrier != netIdentity) continue;
                item.ServerThrow(netIdentity, direction);
                return;
            }
        }

        #endregion

        #region LIFE / RESPAWN

        public void RequestDeath()
        {
            if (isDead || respawnRoutineRunning) return;

            // The server may kill any player directly. Calling the [Command] from the server for a player
            // it does not own is rejected by Mirror, which is why clients never died from server-side hits.
            if (isServer) ServerDie();
            else if (isOwned) CmdRequestDeath();
        }

        [Server]
        public void ServerKill(DeathReason reason)
        {
            netDeathReason = (int)reason;
            ServerDie();
        }

        // The SP stun now throws the held item (PlayerStunnedState -> ThrowHeldObject) and death drops it,
        // both locally. Drop it through the network first, so every copy's SP call finds an empty hand
        // and the server no longer counts this player as the carrier.
        [Server]
        public void ServerDropHeld()
        {
            PlayerInteractionHandler handler = interaction != null ? interaction : GetComponent<PlayerInteractionHandler>();
            ItemScript.CarriableObject_SP held = handler != null ? handler.GetCurrentCarriable() : null;
            if (held != null && held.TryGetComponent(out CarriableObject_MP heldMp)) heldMp.ServerForceDrop(netIdentity);
        }

        // Stun for everyone (car bump, stun zones). Reliable RPCs keep order, so the drop lands first.
        [Server]
        public void ServerStun()
        {
            if (isDead) return;
            ServerDropHeld();
            RpcPlayHitReaction();
        }

        public void RequestStun()
        {
            if (isServer) ServerStun();
            else if (isOwned) CmdRequestStun();
        }

        [Command]
        private void CmdRequestStun() => ServerStun();

        [ClientRpc]
        public void RpcPlayHitReaction()
        {
            if (playerStateMachine == null) return;

            PlayerStates state = playerStateMachine.currentPlayerState;
            if (state == PlayerStates.Stunned || state == PlayerStates.Dead || isDead) return;

            playerStateMachine.ChangeToStunnedState();
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

            // PlayerDeadState.Enter calls the SP DropCarriedObject (local only). Drop through the network
            // first so the item lands for everyone and the server stops treating this player as its carrier.
            ServerDropHeld();

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
                playerStateMachine.SetDeathReason((DeathReason)netDeathReason);
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
