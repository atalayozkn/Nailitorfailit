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
            DogStateMachine dogStateMachine = GetComponentInChildren<DogStateMachine>(true);
            if (dogStateMachine != null) dog = dogStateMachine.transform;
        }

        if (dog == null)
        {
            dog = transform.Find("DogPackage/Dog");

            if (dog == null)
                Debug.LogWarning("[MP-DOG] dog transform not found - dog sync is disabled.");
        }

        if (mailMan == null)
        {
            MailManController mailManController = GetComponentInChildren<MailManController>(true);
            if (mailManController != null) mailMan = mailManController.transform;
        }

        if (mailMan == null)
        {
            mailMan = transform.Find("MailManPackage/MailMan");

            if (mailMan == null)
                Debug.LogWarning("[MP-DOG] mail man transform not found - mail man sync is disabled.");
        }

        if (dog != null && mailMan != null)
            InteractionDebug.Log($"[MP-DOG] companion sync wired (dog='{dog.name}', mailMan='{mailMan.name}').");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (dogBody == null && dog != null) dogBody = dog.GetComponent<Rigidbody>();
        if (dogAnimator == null && dog != null) dogAnimator = dog.GetComponent<Animator>();
        if (mailManAnimator == null && mailMan != null) mailManAnimator = mailMan.GetComponent<Animator>();

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

    private void Update()
    {
        if (dog == null) return;

        if (isOwned)
        {
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

    private void OnDogAnimChanged(int oldHash, int newHash)
    {
        if (isOwned || newHash == 0) return;
        if (dogAnimator == null && dog != null) dogAnimator = dog.GetComponent<Animator>();
        if (dogAnimator != null) dogAnimator.CrossFadeInFixedTime(newHash, 0.15f);
    }

    private void OnMailAnimChanged(int oldHash, int newHash)
    {
        if (isOwned || newHash == 0) return;
        if (mailManAnimator == null && mailMan != null) mailManAnimator = mailMan.GetComponent<Animator>();
        if (mailManAnimator != null) mailManAnimator.CrossFadeInFixedTime(newHash, 0.15f);
    }

    private static int CurrentAnimHash(Animator animator)
    {
        if (animator == null) return 0;
        if (animator.runtimeAnimatorController == null) return 0;
        if (animator.layerCount == 0) return 0;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        return info.fullPathHash;
    }
}
