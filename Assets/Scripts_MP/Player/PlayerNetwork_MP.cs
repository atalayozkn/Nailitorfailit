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

        // The owner's own "knocked down" truth (stunned, getting up, slipping). Every machine runs the stun / stand-up
        // sequence on its own clock from an RPC, so a late or missed step used to leave other players looking at a
        // hit pose long after the owner was walking again. The watchdog below ends that pose when this says "up".
        [SyncVar] private bool netDown;
        private bool lastSentDown;
        private float downLocalSince = -1f;
        private float upSince = -1f;
        private float deadMismatchSince = -1f;
        public static int BodyHealCount;

        // Declared before isDead so it is applied first when both change in the same update.
        [SyncVar] private int netDeathReason = (int)DeathReason.Car;

        [SyncVar(hook = nameof(OnDeadChanged))]
        public bool isDead;

        private bool respawnRoutineRunning;

        // Who this player is: copied from the lobby player when the match starts (the lobby player object is replaced).
        // Name and colour drive the tag above the player and the character's tint; micMuted the icon on the tag.
        [SyncVar] public string playerName = "";
        [SyncVar(hook = nameof(OnColorChanged))] public int colorIndex = -1;
        [SyncVar] public bool micMuted;
        [SyncVar] public bool isHostPlayer; // crown on the name tag

        private void OnColorChanged(int oldValue, int newValue) => MpPlayerTint_MP.Apply(this);

        [Server]
        public void ServerSetIdentity(string displayName, int color, bool muted, bool host)
        {
            playerName = displayName ?? "";
            colorIndex = color;
            micMuted = muted;
            isHostPlayer = host;
        }

        [Command]
        public void CmdSetMicMuted(bool muted)
        {
            micMuted = muted;
        }

        private Rigidbody body;
        private PlayerActiveRagdoll activeRagdoll;

        private DogStateMachine dogAI;
        private bool dogAiWaitingForNavMesh;

        private void Awake()
        {
            // Carry inventory (3 slots) works in multiplayer too: see CarriableObject_MP / PlayerInteractionNetwork_MP.
            PlayerInteractionHandler.NetworkedInventory = true;

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

            // The active ragdoll is real physics: only the owner's machine runs it. Disabled before its Start the
            // component leaves the bones passive, and PlayerStunnedState then uses the classic ragdoll there.
            activeRagdoll = GetComponent<PlayerActiveRagdoll>();
            if (activeRagdoll != null) activeRagdoll.enabled = false;

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

            MpHomeLayout_MP.Apply(transform.root, isHostPlayer);

            if (!Application.isBatchMode) MpNameTag_MP.Attach(this);
            MpPlayerTint_MP.Apply(this);
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
            if (activeRagdoll != null) activeRagdoll.enabled = true;
            stamina.enabled = true;
            if (crashHelper != null) crashHelper.enabled = true;
            if (trapRespawn != null) trapRespawn.enabled = true;

            SetLocalOnlyActive(true);

            // Match chat: translucent, shown only while typing (new messages still float up for a few seconds).
            MpChatSkin_MP.Ensure(this);

            // Bottom-right slot bar. Its icon camera needs a display, so headless test clients skip it.
            if (!Application.isBatchMode && interaction != null) InventoryUI.Create(interaction);

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

            MpAuthoredHud_MP.Bridge(transform.root);

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

            PublishDownState();
            WatchRemoteBody();

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

        private static bool IsDownState(PlayerStates state) =>
            state == PlayerStates.Stunned || state == PlayerStates.Stand || state == PlayerStates.Slipping;

        private void PublishDownState()
        {
            if (!isOwned || stateMachine == null) return;

            bool down = IsDownState(stateMachine.currentPlayerState);
            if (down == lastSentDown) return;

            lastSentDown = down;
            if (isServer) netDown = down;
            else CmdSetDown(down);
        }

        [Command]
        private void CmdSetDown(bool down) => netDown = down;

        // Remote copies only: the owner's state is the truth. If this machine still shows the hit / get-up pose while
        // the owner has been "up" for a moment, or still shows death after a respawn (or the other way round), end it.
        private void WatchRemoteBody()
        {
            if (isOwned || stateMachine == null) return;

            PlayerStates state = stateMachine.currentPlayerState;

            // Down pose with the owner up: the owner needs ~RTT to report "down" after the RPC, so wait for both a
            // local pose that is not brand new and an "up" that has lasted.
            if (IsDownState(state) && !netDown && !isDead)
            {
                if (downLocalSince < 0f) downLocalSince = Time.time;
                if (upSince < 0f) upSince = Time.time;

                if (Time.time - downLocalSince > 1.6f && Time.time - upSince > 0.8f)
                {
                    downLocalSince = upSince = -1f;
                    BodyHealCount++;
                    InteractionDebug.Log($"[MP-HEAL] '{name}' still showed {state} while its owner is up: back to Idle.");
                    stateMachine.ChangeToIdleState();
                    stateMachine.ForceUpdateIdle();
                }
            }
            else
            {
                if (!IsDownState(state)) downLocalSince = -1f;
                upSince = -1f;
            }

            // Death shown here must match the synced flag.
            bool shownDead = state == PlayerStates.Dead;
            if (shownDead != isDead)
            {
                if (deadMismatchSince < 0f) deadMismatchSince = Time.time;
                if (Time.time - deadMismatchSince > 1.0f)
                {
                    deadMismatchSince = -1f;
                    BodyHealCount++;
                    InteractionDebug.Log($"[MP-HEAL] '{name}' death state ({shownDead}) differed from isDead ({isDead}): re-applied.");
                    OnDeadChanged(!isDead, isDead);
                }
            }
            else
            {
                deadMismatchSince = -1f;
            }
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
            ApplyRemoteBones();

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
            if (held == null || !held.TryGetComponent(out CarriableObject_MP _))
            {
                if (emoteThrowAt < 0f)
                {
                    emoteThrowAt = float.MaxValue;
                    InteractionDebug.Log($"[MP-EMOTE] throw emote started but nothing networked in hand (held={(held != null ? held.name : "none")}).");
                }
                else if (emoteThrowAt < float.MaxValue)
                {
                    emoteThrowAt = float.MaxValue;
                    InteractionDebug.Log("[MP-EMOTE] the item left the hand before the throw moment (something cleared the handler).");
                }
                return;
            }

            if (emoteThrowAt < 0f)
            {
                emoteThrowAt = Time.time + EmoteThrowActionTime;
                InteractionDebug.Log($"[MP-EMOTE] throw emote: '{held.name}' will be thrown in {EmoteThrowActionTime:0.00} s.");
                return;
            }

            if (Time.time < emoteThrowAt) return;

            emoteThrowAt = float.MaxValue; // once per emote
            handler.ClearCarriedObject();  // the SP state now finds nothing to throw
            InteractionDebug.Log($"[MP-EMOTE] throwing '{held.name}' (server={isServer}).");
            CmdThrowCarried(transform.forward);
        }

        [Command]
        private void CmdThrowCarried(Vector3 direction)
        {
            InteractionDebug.Log($"[MP-EMOTE] CmdThrowCarried on the server for '{name}'.");
            // The item in hand flies off; stowed ones (same carrier) stay in the inventory.
            PlayerInteractionHandler handler = interaction != null ? interaction : GetComponent<PlayerInteractionHandler>();
            ItemScript.CarriableObject_SP held = handler != null ? handler.GetCurrentCarriable() : null;

            if (held != null && held.TryGetComponent(out CarriableObject_MP heldMp) && heldMp.Carrier == netIdentity)
            {
                heldMp.ServerThrow(netIdentity, direction);
                return;
            }

            foreach (CarriableObject_MP item in FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None))
            {
                if (item.Carrier != netIdentity) continue;
                item.ServerThrow(netIdentity, direction);
                return;
            }
        }

        #endregion

        #region RAGDOLL SYNC

        // Owner: active ragdoll (real physics, may be flung by a car). Everyone else: the classic ragdoll for the
        // same stun (triggered by RpcPlayHitReaction). While stunned the owner sends where its hips are and the
        // others pin their ragdoll's hips to that, so the body lies in the same place on every machine.
        // Limb details stay local (limp limbs), only the body position is shared.
        private Rigidbody ragdollHips;
        private float nextRagdollSendAt;
        private bool hasRagdollPose;
        private Vector3 ragdollPos;
        private Quaternion ragdollRot;

        public bool ActiveRagdollRunning => activeRagdoll != null && activeRagdoll.enabled;

        public Rigidbody RagdollHips
        {
            get
            {
                if (ragdollHips == null && stateMachine != null && stateMachine.ragdollRigidBodies != null)
                {
                    foreach (Rigidbody rb in stateMachine.ragdollRigidBodies)
                    {
                        if (rb == null || !rb.name.EndsWith("Hips")) continue;
                        ragdollHips = rb;
                        break;
                    }
                }

                return ragdollHips;
            }
        }

        private void FixedUpdate()
        {
            Rigidbody hips = stateMachine != null && stateMachine.currentPlayerState == PlayerStates.Stunned ? RagdollHips : null;

            if (hips == null)
            {
                hasRagdollPose = false;
                CaptureRestPose();
                return;
            }

            if (isOwned)
            {
                if (Time.unscaledTime < nextRagdollSendAt) return;
                nextRagdollSendAt = Time.unscaledTime + 0.05f;

                Quaternion[] boneRotations = ReadBoneRotations();

                if (isServer) { RpcRagdollPose(hips.position, hips.rotation); RpcRagdollBones(boneRotations); }
                else { CmdRagdollPose(hips.position, hips.rotation); CmdRagdollBones(boneRotations); }
                return;
            }

            if (!hasRagdollPose || ActiveRagdollRunning) return;

            hips.isKinematic = true; // the other bones hang from it; PlayerStunnedState.Exit releases it

            // A fling (car hit, thrown item) moves the owner's hips metres in a split second. Dragging only the hips
            // that far stretched the jointed limbs across the map (the other player saw a twisted ragdoll): move the
            // whole ragdoll along instead, then keep following with the small smoothing step.
            Vector3 jump = ragdollPos - hips.position;
            if (jump.sqrMagnitude > 1f)
            {
                foreach (Rigidbody bone in stateMachine.ragdollRigidBodies)
                {
                    if (bone == null) continue;

                    bone.position += jump;
                    if (!bone.isKinematic)
                    {
                        bone.linearVelocity = Vector3.zero;
                        bone.angularVelocity = Vector3.zero;
                    }
                }

                hips.rotation = ragdollRot;
                return;
            }

            hips.MovePosition(Vector3.Lerp(hips.position, ragdollPos, 0.5f));
            hips.MoveRotation(Quaternion.Slerp(hips.rotation, ragdollRot, 0.5f));

            CheckRagdollStretch(hips);
        }

        // ---- remote ragdoll reset: limbs stretched / spine twisted by a fling are put back on the rest pose ----
        private Vector3[] restLocalPositions; // each bone's local position (offset from its parent), from a calm pose
        private Vector3[] restOffsets;       // each bone's position in the hips' space, from a calm pose
        private Quaternion[] restRotations;  // each bone's rotation in the hips' space
        private float nextStretchCheckAt;

        // Remembers the rest pose while the player is not ragdolling (the animator keeps the bones in a normal pose).
        private void CaptureRestPose()
        {
            if (restOffsets != null || stateMachine == null || stateMachine.ragdollRigidBodies == null) return;
            if (stateMachine.animator == null || !stateMachine.animator.enabled) return;

            Rigidbody hips = RagdollHips;
            if (hips == null) return;

            var bones = stateMachine.ragdollRigidBodies;
            var offsets = new Vector3[bones.Length];
            var rotations = new Quaternion[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                offsets[i] = hips.transform.InverseTransformPoint(bones[i].position);
                rotations[i] = Quaternion.Inverse(hips.rotation) * bones[i].rotation;
            }

            var locals = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                locals[i] = bones[i] != null ? bones[i].transform.localPosition : Vector3.zero;

            restLocalPositions = locals;
            restOffsets = offsets;
            restRotations = rotations;
        }

        private void CheckRagdollStretch(Rigidbody hips)
        {
            if (restOffsets == null || Time.unscaledTime < nextStretchCheckAt) return;
            nextStretchCheckAt = Time.unscaledTime + 0.2f;

            var bones = stateMachine.ragdollRigidBodies;
            if (bones == null || bones.Length != restOffsets.Length) return;

            bool bad = false;
            for (int i = 0; i < bones.Length && !bad; i++)
            {
                if (bones[i] == null || bones[i] == hips) continue;

                float rest = restOffsets[i].magnitude * Mathf.Abs(hips.transform.lossyScale.x);
                float now = (bones[i].position - hips.position).magnitude;
                if (now > rest * 1.5f + 0.2f) bad = true;
            }

            if (!bad) return;

            ResetRagdollPose(hips);
            BodyHealCount++;
            InteractionDebug.Log($"[MP-HEAL] '{name}' ragdoll limbs were stretched: bones put back on the rest pose.");
        }

        private void ResetRagdollPose(Rigidbody hips)
        {
            var bones = stateMachine.ragdollRigidBodies;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || bones[i] == hips) continue;

                bones[i].position = hips.transform.TransformPoint(restOffsets[i]);
                bones[i].rotation = hips.rotation * restRotations[i];

                if (!bones[i].isKinematic)
                {
                    bones[i].linearVelocity = Vector3.zero;
                    bones[i].angularVelocity = Vector3.zero;
                }
            }
        }

        // ---- the owner's ragdoll SKELETON, not only its hips ----
        // The other machines used to run their own physics on the limbs and only pinned the hips to the owner's: a fling
        // left their limbs stretched or twisted (long legs, a bent spine) while the owner's own ragdoll looked fine.
        // Now the owner's bone rotations are sent too and the other machines pose their (kinematic) bones with them,
        // so the limb lengths can never change and the shape is the owner's.
        private Quaternion[] ragdollBoneTargets;

        private Quaternion[] ReadBoneRotations()
        {
            var bones = stateMachine.ragdollRigidBodies;
            if (bones == null) return new Quaternion[0];

            var rotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                rotations[i] = bones[i] != null ? bones[i].transform.localRotation : Quaternion.identity;

            return rotations;
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdRagdollBones(Quaternion[] rotations) => RpcRagdollBones(rotations);

        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        private void RpcRagdollBones(Quaternion[] rotations)
        {
            if (isOwned) return;
            ragdollBoneTargets = rotations;
        }

        // Every frame, after the animator: while another player's ragdoll is on, its bones are only a pose. Rotations
        // follow the owner's (smoothly, the sync is ~20 Hz); positions are the rest offsets. The positions matter: the
        // bones start as physics bodies for a few frames (before the first pose arrives) and the car, the thrown item
        // or gravity push them off their parent; setting only rotations later left them stretched (long legs).
        private void ApplyRemoteBones()
        {
            if (isOwned || stateMachine == null) return;

            if (stateMachine.currentPlayerState != PlayerStates.Stunned)
            {
                ragdollBoneTargets = null; // stun over: the animator owns the bones again
                return;
            }

            var bones = stateMachine.ragdollRigidBodies;
            if (bones == null) return;

            bool haveRest = restLocalPositions != null && restLocalPositions.Length == bones.Length;
            bool haveTargets = ragdollBoneTargets != null && ragdollBoneTargets.Length == bones.Length;
            Rigidbody hips = RagdollHips;
            float blend = 1f - Mathf.Exp(-18f * Time.deltaTime);

            for (int i = 0; i < bones.Length; i++)
            {
                Rigidbody bone = bones[i];
                if (bone == null || bone == hips) continue;

                if (!bone.isKinematic) bone.isKinematic = true; // pure pose: no physics can stretch a limb
                if (haveRest) bone.transform.localPosition = restLocalPositions[i];
                if (haveTargets) bone.transform.localRotation = Quaternion.Slerp(bone.transform.localRotation, ragdollBoneTargets[i], blend);
            }
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdRagdollPose(Vector3 position, Quaternion rotation) => RpcRagdollPose(position, rotation);

        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        private void RpcRagdollPose(Vector3 position, Quaternion rotation)
        {
            if (isOwned) return;

            ragdollPos = position;
            ragdollRot = rotation;
            hasRagdollPose = true;
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
        // allCarried: death drops the stowed inventory items too; a stun only throws the one in hand (like SP).
        [Server]
        public void ServerDropHeld(bool allCarried = false)
        {
            PlayerInteractionHandler handler = interaction != null ? interaction : GetComponent<PlayerInteractionHandler>();
            ItemScript.CarriableObject_SP held = handler != null ? handler.GetCurrentCarriable() : null;
            if (held != null && held.TryGetComponent(out CarriableObject_MP heldMp)) heldMp.ServerForceDrop(netIdentity);

            if (!allCarried) return;

            foreach (CarriableObject_MP item in FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None))
            {
                if (item.Carrier == netIdentity) item.ServerForceDrop(netIdentity);
            }
        }

        // Stun for everyone (car bump, stun zones). Reliable RPCs keep order, so the drop lands first.
        [Server]
        public void ServerStun()
        {
            if (isDead) return;
            ServerDropHeld();
            RpcPlayHitReaction();
        }

        // Car hit: knocked out like a stun, but thrown: the owner's ragdoll gets a launch velocity, the other machines
        // follow the owner's hips (see RAGDOLL SYNC).
        [Server]
        public void ServerStunFling(Vector3 launchVelocity)
        {
            if (isDead) return;
            ServerStun();
            RpcFling(launchVelocity);
        }

        [ClientRpc]
        private void RpcFling(Vector3 launchVelocity)
        {
            if (isOwned) StartCoroutine(FlingNextStep(launchVelocity));
        }

        // The stun's RPC switched the ragdoll on in this frame; its bodies can take a velocity from the next physics step.
        private IEnumerator FlingNextStep(Vector3 launchVelocity)
        {
            yield return new WaitForFixedUpdate();

            if (stateMachine == null || stateMachine.currentPlayerState != PlayerStates.Stunned) yield break;

            // One impulse on the hips, the joints carry the rest. Giving every ragdoll body the same velocity tore the
            // active ragdoll apart on a car hit: the limbs left the torso, the PD pose never caught up again and the
            // player stayed broken after the stun.
            Rigidbody hips = RagdollHips;
            if (hips != null && !hips.isKinematic)
            {
                hips.linearVelocity = launchVelocity;
                yield break;
            }

            foreach (Rigidbody rb in stateMachine.ragdollRigidBodies)
            {
                if (rb == null || rb.isKinematic) continue;

                rb.linearVelocity = launchVelocity;
                break;
            }
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
            ServerDropHeld(true);

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
            if (rb != null && !rb.isKinematic)
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
