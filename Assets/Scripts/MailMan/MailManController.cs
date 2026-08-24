using UnityEngine;

public class MailManController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private MailManMovementHandler movementHandler;
    [SerializeField] private DogStateMachine stateMachine;


    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 4f;

    [Header("Path")]
    [SerializeField] private Transform[] path;
    [SerializeField] private Transform objectiveTransform;

    private int currentPathIndex;
    private MailBox mailBox;
    //Animation References
    private static readonly int walkHash = Animator.StringToHash("Walk");
    private static readonly int runHash = Animator.StringToHash("Run");
    private static readonly int interactHash = Animator.StringToHash("Interact");
    private static readonly int scaredHash = Animator.StringToHash("Scared");

    private void Awake()
    {
        mailBox = FindFirstObjectByType<MailBox>();
    }
    private void OnEnable()
    {
        if (path == null || path.Length == 0) return;
        currentPathIndex = 0;
        MoveToCurrentPoint();
    }
    private void MoveToCurrentPoint()
    {
        if (currentPathIndex >= path.Length)
        {
            HandlePathComplete();
            return;
        }

        if (currentPathIndex != 0 && path[currentPathIndex - 1] == objectiveTransform)
        {
            HandleObjective();
            return;
        }

        SetRunning(false);
        movementHandler.MoveToTransform(path[currentPathIndex]);

    }
    public void HandleMovementComplete()
    {
        currentPathIndex++;

        if (currentPathIndex >= path.Length)
        {
            HandlePathComplete();
            return;
        }

        MoveToCurrentPoint();
    }
    private void HandlePathComplete()
    {
        stateMachine.StopChase();
        gameObject.SetActive(false);
    }
    private void HandleObjective()
    {
        animator.CrossFadeInFixedTime(interactHash, 0f);
        Invoke(nameof(CompleteObjective), 3.0f);
    }
    private void CompleteObjective()
    {
        mailBox.OnTrigger();
        HandleMovementComplete();
    }
    public void Scare()
    {
        CancelInvoke(nameof(CompleteObjective));
        animator.CrossFadeInFixedTime(scaredHash, 0f);
        movementHandler.StopMovement();
        Invoke(nameof(RunToLastIndex), 2.0f);
    }
    private void RunToLastIndex()
    {
        currentPathIndex = path.Length - 1;
        SetRunning(true);
        movementHandler.MoveToTransform(path[currentPathIndex]);
    }
    public void SetRunning(bool condition)
    {
        if (condition)
        {
            animator.CrossFadeInFixedTime(runHash, 0f);
            movementHandler.SetMoveSpeed(runSpeed);
            return;
        }

        animator.CrossFadeInFixedTime(walkHash, 0f);
        movementHandler.SetMoveSpeed(walkSpeed);
    }
}