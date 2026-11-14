using System.Collections.Generic;
using UnityEngine;

// Hybrid active ragdoll for the singleplayer player.
//
// One skeleton, two passes per frame:
//  1. The Animator poses the bones as usual (Update -> animation).
//  2. LateUpdate reads that pose as the target, writes joint targets, then puts every physics bone back
//     where its Rigidbody really is. What you see is always the physical body chasing the animation.
//
// Muscles are ConfigurableJoint slerp drives. Strength is given as a frequency (Hz) and damping ratio and
// scaled by each limb's inertia, so tuning doesn't depend on the rig's mass values.
// The hips have no joint: they are pulled toward their animated pose relative to the movement root, which
// stays authoritative (PlayerMovement still moves the capsule).
//
// Stun: muscles go to 0 (limp) and the root capsule follows the hips along the ground.
// Stand-up: the get-up clip's first frame is sampled, the root is turned/moved so that frame lines up with
// the lying body, then muscles and the hip pin ramp 0 -> 1, so physics carries the body into the animation
// instead of snapping.
//
// Optional: if this component is missing or disabled, PlayerStunnedState uses the old passive ragdoll.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerActiveRagdoll : MonoBehaviour
{
    [Header("Muscles")]
    [Tooltip("How fast limbs chase the animation. Higher = tighter follow, lower = floppier.")]
    [SerializeField, Range(0.5f, 20f)] private float muscleFrequency = 6f;
    [Tooltip("1 = critically damped. Below 1 overshoots/wobbles, above 1 feels sluggish.")]
    [SerializeField, Range(0.1f, 2f)] private float muscleDampingRatio = 0.9f;
    [Tooltip("Damping kept while limp (fraction of full damping), stops the body acting like a noodle.")]
    [SerializeField, Range(0f, 1f)] private float limpDamping = 0.05f;
    [Tooltip("Arm frequency multiplier while Using/Interacting so tool hits stay precise.")]
    [SerializeField, Range(1f, 6f)] private float toolArmStiffness = 3f;
    [Tooltip("Muscle strength while slipping.")]
    [SerializeField, Range(0f, 1f)] private float slipMuscle = 1f;
    [Tooltip("How fast muscle strength changes outside of the stand-up blend (per second).")]
    [SerializeField] private float muscleChangeSpeed = 4f;

    [Header("Hip Pin")]
    [Tooltip("While in control, hips follow the animation exactly (kinematic). Limbs stay physical. " +
             "Off = hips are only pulled by the pin values below and can sway.")]
    [SerializeField] private bool lockHips = true;
    [SerializeField, Range(0f, 1f)] private float hipPinPosition = 0.85f;
    [SerializeField, Range(0f, 1f)] private float hipPinRotation = 0.6f;
    [Tooltip("If the root jumps further than this (respawn/teleport), the ragdoll is moved along with it.")]
    [SerializeField] private float teleportDistance = 1f;

    [Header("Stand Up")]
    [Tooltip("Get-up animator states (base layer). The one whose first frame best matches the lying body is used.")]
    [SerializeField] private string[] standUpStates = { "Stand" };
    [SerializeField] private float standBlendTime = 0.6f;
    [SerializeField] private AnimationCurve standBlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Ragdoll Bodies")]
    [Tooltip("Layer for limb colliders. Ignore Raycast keeps them out of AI sight lines and ground checks.")]
    [SerializeField] private int ragdollLayer = 2;
    [SerializeField] private int solverIterations = 12;
    [SerializeField] private int solverVelocityIterations = 4;
    [SerializeField] private float maxAngularVelocity = 40f;
    [SerializeField] private bool debugLog;

    private class Bone
    {
        public Transform t;
        public Rigidbody rb;
        public ConfigurableJoint joint;
        public Bone parent;
        public Collider[] colliders;
        public bool isArm;
        public int depth;
        public Quaternion startRel;
        public Quaternion jointSpace;
        public float inertia;
        public Quaternion animRot;
        public Vector3 animPos;
        public float appliedSpring = -1f;
        public float appliedDamper = -1f;
        public Vector3 cachedPos;
        public Quaternion cachedRot;
    }

    private enum Mode { Active, Limp, StandingUp }

    private PlayerStateMachine sm;
    private Animator animator;
    private Rigidbody rootRb;
    private readonly List<Bone> bones = new List<Bone>();
    private readonly HashSet<Collider> ragdollColliders = new HashSet<Collider>();
    private Bone hips;
    private Vector3 hipsAnimLocalPos;
    private Quaternion hipsAnimLocalRot;

    private Mode mode = Mode.Active;
    private float muscle = 1f;
    private float pin = 1f;
    private float armBoost = 1f;
    private float standTimer;
    private bool pendingRecovery;
    private bool snapToAnimation;
    private bool initialized;

    private readonly List<Collider> scratchColliders = new List<Collider>();
    private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();
    private readonly List<Collider> scratchRemove = new List<Collider>();
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private float scanTimer;

    #region SETUP

    private void Awake()
    {
        sm = GetComponent<PlayerStateMachine>();
        animator = sm.animator;
        rootRb = sm.rb;

        // Joints store their reference pose when they initialise, which is the prefab pose. Capture the same
        // pose here, before the Animator has evaluated once.
        var byBody = new Dictionary<Rigidbody, Bone>();
        foreach (var rb in sm.ragdollRigidBodies)
        {
            if (rb == null) continue;
            var b = new Bone { t = rb.transform, rb = rb, joint = rb.GetComponent<ConfigurableJoint>() };
            b.colliders = rb.GetComponents<Collider>();
            string n = rb.name;
            b.isArm = n.Contains("Arm") || n.Contains("Hand") || n.Contains("Shoulder");
            for (var p = b.t; p != null && p != transform; p = p.parent) b.depth++;
            byBody[rb] = b;
            bones.Add(b);
        }
        foreach (var b in bones)
        {
            if (b.joint != null && b.joint.connectedBody != null && byBody.TryGetValue(b.joint.connectedBody, out var parent))
                b.parent = parent;
            else if (hips == null || b.depth < hips.depth)
                hips = b;
        }
        bones.Sort((a, c) => a.depth.CompareTo(c.depth));

        foreach (var b in bones)
        {
            if (b.parent == null) continue;
            b.startRel = Quaternion.Inverse(b.parent.t.rotation) * b.t.rotation;
            Vector3 right = b.joint.axis.normalized;
            Vector3 forward = Vector3.Cross(b.joint.axis, b.joint.secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            b.jointSpace = Quaternion.LookRotation(forward, up);
        }

        if (hips != null)
        {
            hipsAnimLocalPos = hips.t.localPosition;
            hipsAnimLocalRot = hips.t.localRotation;
        }
    }

    private void Start()
    {
        if (hips == null || animator == null || rootRb == null)
        {
            Debug.LogWarning("[PlayerActiveRagdoll] Missing hips/animator/root body, disabled.", this);
            enabled = false;
            return;
        }

        // PlayerStateMachine.Awake switched the ragdoll off (kinematic, no colliders). Turn it into a live body.
        foreach (var b in bones)
        {
            b.rb.isKinematic = false;
            b.rb.useGravity = true;
            b.rb.detectCollisions = true;
            b.rb.interpolation = RigidbodyInterpolation.None;
            b.rb.solverIterations = solverIterations;
            b.rb.solverVelocityIterations = solverVelocityIterations;
            b.rb.maxAngularVelocity = maxAngularVelocity;
            b.rb.linearVelocity = Vector3.zero;
            b.rb.angularVelocity = Vector3.zero;
            b.t.gameObject.layer = ragdollLayer;
            foreach (var c in b.colliders)
            {
                c.enabled = true;
                ragdollColliders.Add(c);
            }
            if (b.joint != null)
            {
                b.joint.enableCollision = false;
                b.joint.rotationDriveMode = RotationDriveMode.Slerp;
            }
        }

        ComputeInertia();
        SetSelfCollision(false);
        ScanPlayerColliders();
        initialized = true;
    }

    // Effective inertia of each joint: everything hanging below it, about its anchor. Used to turn a
    // frequency into a spring so a hand and a whole leg feel equally stiff.
    private void ComputeInertia()
    {
        foreach (var b in bones)
        {
            if (b.parent == null) continue;
            Vector3 anchor = b.t.position;
            float sum = 0f;
            foreach (var other in bones)
            {
                if (!IsInChain(other, b)) continue;
                Vector3 it = other.rb.inertiaTensor;
                sum += Mathf.Max(it.x, Mathf.Max(it.y, it.z));
                sum += other.rb.mass * (other.rb.worldCenterOfMass - anchor).sqrMagnitude;
            }
            b.inertia = Mathf.Max(sum, 1e-5f);
        }
    }

    private static bool IsInChain(Bone b, Bone ancestor)
    {
        for (var p = b; p != null; p = p.parent) if (p == ancestor) return true;
        return false;
    }

    #endregion

    #region PUBLIC API (called by PlayerStunnedState)

    public void EnterLimp()
    {
        mode = Mode.Limp;
        muscle = 0f;
        pin = 0f;
        pendingRecovery = false;
        SetHipsKinematic(false);
        SetSelfCollision(true);
        ApplyDrives();
    }

    // Stun ended. Next Update decides between a smooth stand-up (Stand state) and an instant recover.
    public void ExitLimp()
    {
        pendingRecovery = true;
    }

    #endregion

    #region LOOP

    private void Update()
    {
        if (!initialized || !pendingRecovery) return;
        pendingRecovery = false;

        if (sm.currentPlayerState == PlayerStates.Stand) BeginStandUp();
        else HardRecover();
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        float dt = Time.fixedDeltaTime;

        scanTimer -= dt;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.1f;
            ScanPlayerColliders();
        }

        SyncTransformsWithBodies();

        switch (mode)
        {
            case Mode.Limp:
                FollowRootToHips(dt);
                break;

            case Mode.StandingUp:
                standTimer += dt;
                float w = standBlendCurve.Evaluate(Mathf.Clamp01(standTimer / Mathf.Max(standBlendTime, 0.01f)));
                muscle = w;
                pin = w;
                if (standTimer >= standBlendTime) mode = Mode.Active;
                break;

            case Mode.Active:
                float targetMuscle = sm.currentPlayerState == PlayerStates.Slipping ? slipMuscle : 1f;
                muscle = Mathf.MoveTowards(muscle, targetMuscle, muscleChangeSpeed * dt);
                pin = Mathf.MoveTowards(pin, 1f, muscleChangeSpeed * dt);
                break;
        }

        bool toolUse = sm.currentPlayerState == PlayerStates.Using || sm.currentPlayerState == PlayerStates.Interacting;
        armBoost = Mathf.MoveTowards(armBoost, toolUse && mode == Mode.Active ? toolArmStiffness : 1f, 8f * dt);

        ApplyDrives();
        if (pin > 0f) PinHips(dt);
    }

    private void LateUpdate()
    {
        if (!initialized) return;

        // 1. Read the pose the Animator just wrote.
        foreach (var b in bones)
        {
            b.animRot = b.t.rotation;
            b.animPos = b.t.position;
        }
        hipsAnimLocalPos = hips.t.localPosition;
        hipsAnimLocalRot = hips.t.localRotation;

        // 2. Joint targets from that pose. Don't touch anchors/axes here: changing them at runtime rebuilds the
        //    joint frame from the current pose and breaks the reference the targets are measured against.
        foreach (var b in bones)
        {
            if (b.parent == null) continue;
            Quaternion targetRel = Quaternion.Inverse(b.parent.animRot) * b.animRot;
            b.joint.targetRotation = Quaternion.Inverse(b.jointSpace) * (Quaternion.Inverse(targetRel) * b.startRel) * b.jointSpace;
        }

        // 3. Either jump the bodies onto the animation, or show where physics really has them.
        if (snapToAnimation)
        {
            snapToAnimation = false;
            foreach (var b in bones)
            {
                b.rb.position = b.animPos;
                b.rb.rotation = b.animRot;
                if (b.rb.isKinematic) continue;
                b.rb.linearVelocity = rootRb.linearVelocity;
                b.rb.angularVelocity = Vector3.zero;
            }
            return;
        }

        foreach (var b in bones) b.t.SetPositionAndRotation(b.rb.position, b.rb.rotation);
    }

    #endregion

    #region MUSCLES & PIN

    private void ApplyDrives()
    {
        float zeta = muscleDampingRatio;
        foreach (var b in bones)
        {
            if (b.parent == null) continue;
            float omega = 2f * Mathf.PI * muscleFrequency * (b.isArm ? armBoost : 1f);
            float spring = b.inertia * omega * omega * muscle;
            float damper = 2f * zeta * b.inertia * omega * Mathf.Max(muscle, limpDamping);

            if (Mathf.Abs(spring - b.appliedSpring) < 1e-4f + b.appliedSpring * 0.01f &&
                Mathf.Abs(damper - b.appliedDamper) < 1e-4f + b.appliedDamper * 0.01f) continue;

            b.appliedSpring = spring;
            b.appliedDamper = damper;
            b.joint.slerpDrive = new JointDrive { positionSpring = spring, positionDamper = damper, maximumForce = float.MaxValue };
        }
    }

    // Velocity tracking toward the animated hips, offset by the root's motion this step so the body
    // doesn't trail one physics step behind the capsule.
    private void PinHips(float dt)
    {
        Transform hipsParent = hips.t.parent;
        Vector3 targetPos = hipsParent.TransformPoint(hipsAnimLocalPos) + rootRb.linearVelocity * dt;
        Quaternion targetRot = hipsParent.rotation * hipsAnimLocalRot;

        // Fully in control: hips locked onto the animation. Only during the stand-up blend (pin < 1) do the
        // hips stay physical so they can be eased in.
        bool locked = lockHips && mode == Mode.Active && pin >= 0.999f;
        SetHipsKinematic(locked);
        if (locked)
        {
            hips.rb.MovePosition(targetPos);
            hips.rb.MoveRotation(targetRot);
            return;
        }

        Vector3 desiredVel = (targetPos - hips.rb.position) / dt;
        hips.rb.linearVelocity = Vector3.Lerp(hips.rb.linearVelocity, desiredVel, hipPinPosition * pin);

        Quaternion delta = targetRot * Quaternion.Inverse(hips.rb.rotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (float.IsFinite(axis.x) && Mathf.Abs(angle) > 0.01f)
        {
            Vector3 desiredAng = axis.normalized * (angle * Mathf.Deg2Rad / dt);
            hips.rb.angularVelocity = Vector3.Lerp(hips.rb.angularVelocity, desiredAng, hipPinRotation * pin);
        }
    }

    private void SetHipsKinematic(bool kinematic)
    {
        if (hips.rb.isKinematic == kinematic) return;
        hips.rb.isKinematic = kinematic;
        // Kinematic -> dynamic starts at rest; carry the capsule's motion so a hit doesn't make the body just drop.
        if (!kinematic) hips.rb.linearVelocity = rootRb.linearVelocity;
    }

    // While limp, drag the (dynamic) root capsule along under the hips so the camera and the later stand-up
    // stay with the body. Horizontal only; gravity handles height.
    private void FollowRootToHips(float dt)
    {
        Vector3 to = hips.rb.position - rootRb.position;
        Vector3 v = rootRb.linearVelocity;
        v.x = to.x / dt * 0.5f;
        v.z = to.z / dt * 0.5f;
        rootRb.linearVelocity = v;
    }

    #endregion

    #region STAND UP / RECOVER

    private void BeginStandUp()
    {
        Quaternion ragRot = hips.rb.rotation;
        Vector3 ragPos = hips.rb.position;

        int bestHash = 0;
        float bestErr = float.MaxValue;
        Quaternion bestYaw = transform.rotation;
        Vector3 bestRelPos = Vector3.zero;

        // Sample each get-up clip's first frame and see how well it fits the lying body after a yaw turn.
        foreach (string state in standUpStates)
        {
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) continue;

            animator.Play(hash, 0, 0f);
            animator.Update(0f);

            Quaternion relRot = Quaternion.Inverse(transform.rotation) * hips.t.rotation;
            Vector3 relPos = Quaternion.Inverse(transform.rotation) * (hips.t.position - transform.position);
            Quaternion yaw = YawOnly(ragRot * Quaternion.Inverse(relRot));
            float err = Quaternion.Angle(yaw * relRot, ragRot);

            if (err < bestErr)
            {
                bestErr = err;
                bestHash = hash;
                bestYaw = yaw;
                bestRelPos = relPos;
            }
        }

        if (bestHash == 0)
        {
            if (debugLog) Debug.LogWarning("[PlayerActiveRagdoll] No stand-up state found, recovering instantly.", this);
            HardRecover();
            return;
        }

        animator.Play(bestHash, 0, 0f);
        animator.Update(0f);

        Vector3 newRootPos = ragPos - bestYaw * bestRelPos;
        if (TryGround(ragPos, out float groundY)) newRootPos.y = groundY;
        MoveRootKeepingRagdoll(newRootPos, bestYaw);

        if (debugLog) Debug.Log($"[PlayerActiveRagdoll] Stand-up pose mismatch {bestErr:F0} deg (above ~45 means the clip doesn't start from this lying pose).", this);

        SetSelfCollision(false);
        mode = Mode.StandingUp;
        standTimer = 0f;
        muscle = 0f;
        pin = 0f;
    }

    // Leaving the stun for anything other than Stand (death, forced idle): jump to the animation.
    private void HardRecover()
    {
        SetSelfCollision(false);
        mode = Mode.Active;
        muscle = 1f;
        pin = 1f;
        snapToAnimation = true;
    }

    private void MoveRootKeepingRagdoll(Vector3 pos, Quaternion rot)
    {
        foreach (var b in bones)
        {
            b.cachedPos = b.rb.position;
            b.cachedRot = b.rb.rotation;
        }

        transform.SetPositionAndRotation(pos, rot);
        rootRb.position = pos;
        rootRb.rotation = rot;
        rootRb.linearVelocity = Vector3.zero;
        rootRb.angularVelocity = Vector3.zero;

        foreach (var b in bones)
        {
            b.t.SetPositionAndRotation(b.cachedPos, b.cachedRot);
            b.rb.position = b.cachedPos;
            b.rb.rotation = b.cachedRot;
        }
        Physics.SyncTransforms();
    }

    private bool TryGround(Vector3 from, out float y)
    {
        y = 0f;
        int count = Physics.RaycastNonAlloc(from + Vector3.up * 0.5f, Vector3.down, groundHits, 3f, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var h = groundHits[i];
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                y = h.point.y;
            }
        }
        return best < float.MaxValue;
    }

    private static Quaternion YawOnly(Quaternion q)
    {
        Vector3 f = q * Vector3.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-4f)
        {
            f = q * Vector3.up;
            f.y = 0f;
        }
        return f.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    #endregion

    #region COLLISION BOOKKEEPING

    // Normally the physics write-back keeps bone transforms on their bodies. If the root was teleported
    // (respawn sets transform.position), the bones moved with it: carry the bodies along instead of
    // pulling the bones back.
    private void SyncTransformsWithBodies()
    {
        bool teleported = (hips.t.position - hips.rb.position).sqrMagnitude > teleportDistance * teleportDistance;
        foreach (var b in bones)
        {
            if (teleported)
            {
                b.rb.position = b.t.position;
                b.rb.rotation = b.t.rotation;
                if (b.rb.isKinematic) continue;
                b.rb.linearVelocity = Vector3.zero;
                b.rb.angularVelocity = Vector3.zero;
            }
            else if ((b.t.position - b.rb.position).sqrMagnitude > 1e-8f)
            {
                b.t.SetPositionAndRotation(b.rb.position, b.rb.rotation);
            }
        }
        if (teleported && mode == Mode.Limp) HardRecover();
    }

    // Limbs ignore each other while animated (animations overlap limbs all the time) and collide while limp.
    // Directly jointed pairs never collide (joint.enableCollision = false).
    private void SetSelfCollision(bool collide)
    {
        for (int i = 0; i < bones.Count; i++)
            for (int j = i + 1; j < bones.Count; j++)
            {
                bool adjacent = bones[i].parent == bones[j] || bones[j].parent == bones[i];
                foreach (var a in bones[i].colliders)
                    foreach (var c in bones[j].colliders)
                        Physics.IgnoreCollision(a, c, !collide || adjacent);
            }
    }

    // Everything else under the player (root capsule, crash helper, held/carried items) must not push the
    // limbs. Re-applied regularly because toggling a collider resets its ignore state. Items that leave
    // the player (dropped/thrown) collide with the limbs again.
    private void ScanPlayerColliders()
    {
        GetComponentsInChildren(true, scratchColliders);
        foreach (var c in scratchColliders)
        {
            if (ragdollColliders.Contains(c)) continue;
            foreach (var r in ragdollColliders) Physics.IgnoreCollision(r, c, true);
            ignoredColliders.Add(c);
        }

        scratchRemove.Clear();
        foreach (var c in ignoredColliders)
            if (c == null || !c.transform.IsChildOf(transform)) scratchRemove.Add(c);
        foreach (var c in scratchRemove)
        {
            if (c != null) foreach (var r in ragdollColliders) Physics.IgnoreCollision(r, c, false);
            ignoredColliders.Remove(c);
        }
    }

    #endregion
}
