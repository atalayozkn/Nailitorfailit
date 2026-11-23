using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public class DogSync_MP : NetworkBehaviour
{
    [Header("Dog")]
    [SerializeField] private Transform dog;
    [SerializeField] private Rigidbody dogBody;

    [Header("Mail man (optional)")]
    [SerializeField] private Transform mailMan;

    [Header("Send (owner -> server)")]
    [SerializeField] private float sendInterval = 0.06f;
    [SerializeField] private float positionThreshold = 0.005f;
    [SerializeField] private float rotationThreshold = 0.5f;

    [Header("Interpolation")]
    [SerializeField] private float extraBufferSeconds = 0.08f;
    [SerializeField] private float maxBufferSeconds = 0.6f;

    private Animator dogAnimator;
    private Animator mailManAnimator;

    private MailManController mailManController;
    private MailManMovementHandler mailManMovement;
    private bool mailManGated;

    private double lastSendTime;
    private double lastSnapshotStamp;
    private Vector3 lastSeenNetPos;
    private Quaternion lastSeenNetRot;
    private Vector3 lastSeenMailPos;
    private Quaternion lastSeenMailRot;

    private int lastDogAnimHash;
    private int lastMailAnimHash;

    [SyncVar(hook = nameof(OnNetDogPositionChanged))] private Vector3 netLocalPosition;
    [SyncVar(hook = nameof(OnNetDogRotationChanged))] private Quaternion netLocalRotation;
    [SyncVar(hook = nameof(OnNetMailPositionChanged))] private Vector3 netMailLocalPosition;
    [SyncVar(hook = nameof(OnNetMailRotationChanged))] private Quaternion netMailLocalRotation;
    [SyncVar(hook = nameof(OnDogAnimChanged))] private int netDogAnimStateHash;
    [SyncVar(hook = nameof(OnMailAnimChanged))] private int netMailAnimStateHash;

    // The host's dog bars (satiety, favor, energy, bowl). Every machine ran its own dog's timers, so a client's bars drifted away
    // from the host's dog (and never showed the host's feeding). The host's player object publishes its numbers; the other
    // machines write them over their own (hidden) dog's controllers and sliders.
    [SyncVar] private bool netBarsPublished;
    [SyncVar] private float netSatiety;
    [SyncVar] private float netFavor;
    [SyncVar] private float netEnergy;
    [SyncVar] private int netBowlFood;
    private float nextBarsAt;

    private void SyncBars()
    {
        if (dog == null || Time.unscaledTime < nextBarsAt) return;
        nextBarsAt = Time.unscaledTime + 0.25f;

        if (VersusMatch.Instance != null) return; // Compete: everybody has their own dog

        if (isServer && isOwned)
        {
            var bowl = transform.root.GetComponentInChildren<FoodBowl>(true);
            netSatiety = MpSharedCompat.MpGetField(dog.GetComponent<SatietyController>(), "currentSatiety", netSatiety);
            netFavor = MpSharedCompat.MpGetField(dog.GetComponent<FavorController>(), "currentFavor", (int)netFavor);
            netEnergy = MpSharedCompat.MpGetField(dog.GetComponent<DogEnergyController>(), "currentEnergy", netEnergy);
            if (bowl != null) netBowlFood = MpSharedCompat.MpGetField(bowl, "currentFood", netBowlFood);
            netBarsPublished = true;
            return;
        }

        if (isServer || isOwned || !netBarsPublished) return;

        // Another machine's copy of the host's player: write its numbers onto THIS machine's own dog.
        DogSync_MP mine = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponentInChildren<DogSync_MP>(true) : null;
        if (mine == null || mine == this || mine.dog == null) return;

        ApplyBar(mine.dog.GetComponent<SatietyController>(), "currentSatiety", netSatiety);
        ApplyBar(mine.dog.GetComponent<FavorController>(), "currentFavor", netFavor);
        ApplyBar(mine.dog.GetComponent<DogEnergyController>(), "currentEnergy", netEnergy);
        ApplyBar(mine.transform.root.GetComponentInChildren<FoodBowl>(true), "currentFood", netBowlFood);
    }

    private static void ApplyBar(MonoBehaviour bar, string field, float value)
    {
        if (bar == null) return;

        var info = bar.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (info == null) return;

        if (info.FieldType == typeof(int)) info.SetValue(bar, Mathf.RoundToInt(value));
        else info.SetValue(bar, value);

        var sliderInfo = bar.GetType().GetField("slider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (sliderInfo?.GetValue(bar) is UnityEngine.UI.Slider slider) slider.value = value;
    }

    private class PoseTrack
    {
        private struct Snapshot { public double time; public Vector3 pos; public Quaternion rot; }

        private readonly List<Snapshot> snapshots = new List<Snapshot>();
        private double lastStamp;

        public void Buffer(Vector3 pos, Quaternion rot)
        {
            double now = NetworkTime.time;
            if (snapshots.Count > 0 && now - lastStamp < 0.0005) return;
            lastStamp = now;
            snapshots.Add(new Snapshot { time = now, pos = pos, rot = rot });

            while (snapshots.Count > 2 && now - snapshots[0].time > 1.5)
                snapshots.RemoveAt(0);
        }

        public void Apply(Transform target, float extraBuffer, float maxBuffer, float smoothingRate)
        {
            if (target == null) return;

            if (snapshots.Count == 0) return;

            if (snapshots.Count == 1)
            {
                Smooth(target, snapshots[0].pos, snapshots[0].rot, smoothingRate);
                return;
            }

            float buffer = Mathf.Clamp((float)NetworkTime.rtt + extraBuffer, 0.1f, maxBuffer);
            double renderTime = NetworkTime.time - buffer;

            while (snapshots.Count >= 2 && renderTime > snapshots[1].time)
                snapshots.RemoveAt(0);

            if (snapshots.Count == 1)
            {
                Smooth(target, snapshots[0].pos, snapshots[0].rot, smoothingRate);
                return;
            }

            Snapshot from = snapshots[0];
            Snapshot to = snapshots[1];
            double span = to.time - from.time;
            float t = span > 0.0001 ? Mathf.Clamp01((float)((renderTime - from.time) / span)) : 1f;

            target.localPosition = Vector3.LerpUnclamped(from.pos, to.pos, t);
            target.localRotation = Quaternion.SlerpUnclamped(from.rot, to.rot, t);
        }

        private static void Smooth(Transform target, Vector3 pos, Quaternion rot, float rate)
        {
            float factor = 1f - Mathf.Exp(-rate * Time.deltaTime);
            target.localPosition = Vector3.Lerp(target.localPosition, pos, factor);
            target.localRotation = Quaternion.Slerp(target.localRotation, rot, factor);
        }
    }

    private readonly PoseTrack dogTrack = new PoseTrack();
    private readonly PoseTrack mailTrack = new PoseTrack();

    private void Awake()
    {
        if (dog == null)
        {
            DogStateMachine dogStateMachine = transform.root.GetComponentInChildren<DogStateMachine>(true);
            if (dogStateMachine != null) dog = dogStateMachine.transform;
        }

        if (dog == null)
        {
            dog = transform.root.Find("DogPackage/Dog");

            if (dog == null)
                Debug.LogWarning("[MP-DOG] dog transform not found - dog sync is disabled.");
        }

        if (mailMan == null)
        {
            MailManController mailManController = transform.root.GetComponentInChildren<MailManController>(true);
            if (mailManController != null) mailMan = mailManController.transform;
        }

        if (mailMan == null)
        {
            mailMan = transform.root.Find("MailManPackage/MailMan");

            if (mailMan == null)
                Debug.LogWarning("[MP-DOG] mail man transform not found - mail man sync is disabled.");
        }

        if (mailMan != null)
        {
            mailMan.TryGetComponent(out mailManController);
            mailMan.TryGetComponent(out mailManMovement);
        }

        if (dog != null && mailMan != null)
            InteractionDebug.Log($"[MP-DOG] companion sync wired (dog='{dog.name}', mailMan='{mailMan.name}').");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (dogBody == null && dog != null) dogBody = dog.GetComponent<Rigidbody>();
        if (dogAnimator == null && dog != null) dogAnimator = FindAnimator(dog);
        if (mailManAnimator == null && mailMan != null) mailManAnimator = FindAnimator(mailMan);

        if (isOwned) return;

        if (dog != null)
        {
            if (dogBody != null) dogBody.isKinematic = true;

            if (dog.TryGetComponent(out DogMovementHandler dogMovement))
            {
                dogMovement.StopAllMovement();
                dogMovement.enabled = false;
            }

            if (dog.TryGetComponent(out NavMeshAgent dogAgent))
            {
                if (dogAgent.enabled && dogAgent.isOnNavMesh) dogAgent.ResetPath();
                dogAgent.enabled = false;
            }
        }

        if (mailMan != null)
        {
            if (mailMan.TryGetComponent(out MailManController controllerTravel)) controllerTravel.enabled = false;

            if (mailMan.TryGetComponent(out MailManMovementHandler movement))
            {
                movement.StopMovement();
                movement.enabled = false;
            }

            if (mailMan.TryGetComponent(out NavMeshAgent mailAgent))
            {
                if (mailAgent.enabled && mailAgent.isOnNavMesh) mailAgent.ResetPath();
                mailAgent.enabled = false;
            }
        }
    }

    private void GateMailManUntilNavMesh()
    {
        if (mailManController == null && mailManMovement == null) return;

        if (!MpLevelInstaller.NavMeshReady)
        {
            if (mailManController != null) mailManController.enabled = false;
            if (mailManMovement != null) mailManMovement.enabled = false;

            mailManGated = true;
            return;
        }

        if (!mailManGated) return;

        mailManGated = false;

        if (mailManController != null) mailManController.enabled = true;
        if (mailManMovement != null) mailManMovement.enabled = true;

        InteractionDebug.Log("[MP-DOG] NavMesh ready - mail man movement re-enabled.");
    }

    // ---- dog bar diagnostics (owner): values, sliders and enable resets, every 15 s in Player.log ----
    private float nextBarLogAt;
    private float lastSatiety = -1f;
    private int barResets;

    private static float BarValue(MonoBehaviour bar, string field)
    {
        if (bar == null) return -1f;
        var info = bar.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        object raw = info?.GetValue(bar);
        return raw is float f ? f : raw is int i ? i : -1f;
    }

    private static string SliderOf(MonoBehaviour bar)
    {
        if (bar == null) return "-";
        var info = bar.GetType().GetField("slider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var slider = info?.GetValue(bar) as UnityEngine.UI.Slider;
        return slider == null ? "none" : $"{slider.value:0}/{slider.maxValue:0}{(slider.gameObject.activeInHierarchy ? "" : " hidden")}";
    }

    private void LogDogBars()
    {
        var satiety = dog.GetComponent<SatietyController>();
        var favor = dog.GetComponent<FavorController>();
        var energy = dog.GetComponent<DogEnergyController>();

        float s = BarValue(satiety, "currentSatiety");
        if (lastSatiety >= 0f && s > lastSatiety + 5f) barResets++; // jumped up without a meal => OnEnable reset
        lastSatiety = s;

        if (Time.unscaledTime < nextBarLogAt) return;
        nextBarLogAt = Time.unscaledTime + 15f;

        Debug.Log($"[MP-DOG] bars: satiety={s:0} ({SliderOf(satiety)}, {(satiety != null && satiety.isActiveAndEnabled ? "on" : "OFF")}) " +
                  $"favor={BarValue(favor, "currentFavor"):0} ({SliderOf(favor)}, {(favor != null && favor.isActiveAndEnabled ? "on" : "OFF")}) " +
                  $"energy={BarValue(energy, "currentEnergy"):0} ({SliderOf(energy)}, {(energy != null && energy.isActiveAndEnabled ? "on" : "OFF")}) " +
                  $"dogActive={dog.gameObject.activeInHierarchy} jumpsUp={barResets} timeScale={Time.timeScale}");
    }

    private void Update()
    {
        if (dog == null) return;

        SyncBars();

        if (isOwned)
        {
            GateMailManUntilNavMesh();
            LogDogBars();

            if ((NetworkTime.time - lastSendTime) >= sendInterval)
            {
                bool dogMoved = (dog.localPosition - netLocalPosition).sqrMagnitude > positionThreshold * positionThreshold
                                || Quaternion.Angle(dog.localRotation, netLocalRotation) > rotationThreshold;

                bool mailMoved = mailMan != null &&
                                 ((mailMan.localPosition - netMailLocalPosition).sqrMagnitude > positionThreshold * positionThreshold
                                  || Quaternion.Angle(mailMan.localRotation, netMailLocalRotation) > rotationThreshold);

                if (dogMoved || mailMoved)
                {
                    lastSendTime = NetworkTime.time;
                    CmdSetPoses(dog.localPosition, dog.localRotation,
                        mailMan != null ? mailMan.localPosition : Vector3.zero,
                        mailMan != null ? mailMan.localRotation : Quaternion.identity);
                }
            }

            int dogHash = CurrentAnimHash(dogAnimator);
            int mailHash = CurrentAnimHash(mailManAnimator);

            if (dogHash != lastDogAnimHash || mailHash != lastMailAnimHash)
            {
                lastDogAnimHash = dogHash;
                lastMailAnimHash = mailHash;
                CmdSetAnimHashes(dogHash, mailHash);
            }

            return;
        }

        KeepAnimationInSync();

        float rate = 1f / Mathf.Max(0.05f, sendInterval + 0.05f);
        dogTrack.Apply(dog, extraBufferSeconds, maxBufferSeconds, rate);

        if (mailMan != null)
            mailTrack.Apply(mailMan, extraBufferSeconds, maxBufferSeconds, rate);

        if (isServer)
        {
            if (lastSeenNetPos != netLocalPosition || lastSeenNetRot != netLocalRotation)
            {
                lastSeenNetPos = netLocalPosition;
                lastSeenNetRot = netLocalRotation;
                dogTrack.Buffer(netLocalPosition, netLocalRotation);
            }

            if (lastSeenMailPos != netMailLocalPosition || lastSeenMailRot != netMailLocalRotation)
            {
                lastSeenMailPos = netMailLocalPosition;
                lastSeenMailRot = netMailLocalRotation;
                mailTrack.Buffer(netMailLocalPosition, netMailLocalRotation);
            }

            dogTrack.Apply(dog, extraBufferSeconds, maxBufferSeconds, rate);
            if (mailMan != null) mailTrack.Apply(mailMan, extraBufferSeconds, maxBufferSeconds, rate);
        }
    }

    [Command]
    private void CmdSetPoses(Vector3 dogPos, Quaternion dogRot, Vector3 mailPos, Quaternion mailRot)
    {
        netLocalPosition = dogPos;
        netLocalRotation = dogRot;
        netMailLocalPosition = mailPos;
        netMailLocalRotation = mailRot;
    }

    [Command]
    private void CmdSetAnimHashes(int dogHash, int mailHash)
    {
        netDogAnimStateHash = dogHash;
        netMailAnimStateHash = mailHash;
    }

    private void OnNetDogPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        if (isServer || isOwned) return;
        dogTrack.Buffer(netLocalPosition, netLocalRotation);
    }

    private void OnNetDogRotationChanged(Quaternion oldRot, Quaternion newRot)
    {
        if (isServer || isOwned) return;
        dogTrack.Buffer(netLocalPosition, netLocalRotation);
    }

    private void OnNetMailPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        if (isServer || isOwned) return;
        mailTrack.Buffer(netMailLocalPosition, netMailLocalRotation);
    }

    private void OnNetMailRotationChanged(Quaternion oldRot, Quaternion newRot)
    {
        if (isServer || isOwned) return;
        mailTrack.Buffer(netMailLocalPosition, netMailLocalRotation);
    }

    // The animation hashes arrive only when they CHANGE. The mail man is switched on later than his first hash was
    // sent (he starts inactive), so a machine that had not seen him yet stood him in a T-pose until the next change.
    // Every frame: if what the animator plays differs from the synced state, play the synced one.
    private float nextAnimCheckAt;

    private void KeepAnimationInSync()
    {
        if (Time.unscaledTime < nextAnimCheckAt) return;
        nextAnimCheckAt = Time.unscaledTime + 0.25f;

        FollowSynced(dogAnimator, netDogAnimStateHash);

        if (mailManAnimator == null && mailMan != null) mailManAnimator = FindAnimator(mailMan);
        FollowSynced(mailManAnimator, netMailAnimStateHash);
    }

    private static void FollowSynced(Animator animator, int stateHash)
    {
        if (animator == null || stateHash == 0) return;
        if (animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled || !animator.isInitialized) return;
        if (animator.layerCount == 0 || animator.IsInTransition(0)) return;
        if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash == stateHash) return;
        if (!animator.HasState(0, stateHash)) return;

        animator.CrossFadeInFixedTime(stateHash, 0.15f);
    }

    private void OnDogAnimChanged(int oldHash, int newHash)
    {
        if (isOwned || newHash == 0) return;
        if (dogAnimator == null && dog != null) dogAnimator = FindAnimator(dog);
        if (dogAnimator != null) dogAnimator.CrossFadeInFixedTime(newHash, 0.15f);
    }

    private void OnMailAnimChanged(int oldHash, int newHash)
    {
        if (isOwned || newHash == 0) return;
        if (mailManAnimator == null && mailMan != null) mailManAnimator = FindAnimator(mailMan);
        if (mailManAnimator != null) mailManAnimator.CrossFadeInFixedTime(newHash, 0.15f);
    }

    // The animator of the golden dog / the mail man sits on a child model, not on the object that moves. Looking only
    // on the moving object found nothing, so no animation state was ever sent or played on the other machines.
    private static Animator FindAnimator(Transform root)
    {
        if (root == null) return null;

        Animator found = root.GetComponent<Animator>();
        if (found != null && found.runtimeAnimatorController != null) return found;

        foreach (Animator candidate in root.GetComponentsInChildren<Animator>(true))
            if (candidate.runtimeAnimatorController != null) return candidate;

        return found;
    }

    private static int CurrentAnimHash(Animator animator)
    {
        if (animator == null) return 0;
        if (animator.runtimeAnimatorController == null) return 0;

        // A disabled/inactive animator is not playing its controller; layerCount then logs an error every frame.
        if (!animator.isActiveAndEnabled || !animator.isInitialized) return 0;
        if (animator.layerCount == 0) return 0;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.fullPathHash;
    }
}
