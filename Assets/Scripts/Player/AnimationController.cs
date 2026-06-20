using Mirror;
using UnityEngine;
using System.Collections;

public class AnimationController : NetworkBehaviour
{
    [SerializeField] private float updateRate = 0.05f;

    private Animator animator;
    private ICharacterState characterState;
    private Rigidbody rb;
    private CharacterController cc;

    private Coroutine animationRoutine;

    private void Start()
    {
        animator = GetComponent<Animator>();

        characterState = GetComponent<ICharacterState>();
        if (characterState == null)
            characterState = GetComponentInParent<ICharacterState>();

        rb = GetComponentInParent<Rigidbody>();
        cc = GetComponentInParent<CharacterController>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (animationRoutine == null)
            animationRoutine = StartCoroutine(AnimationUpdateRoutine());
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
    }

    private IEnumerator AnimationUpdateRoutine()
    {
        while (true)
        {
            UpdateAnimatorValues();
            yield return new WaitForSeconds(updateRate);
        }
    }

    private void UpdateAnimatorValues()
    {
        if (animator == null) return;

        float currentSpeed = 0f;

        if (rb != null)
        {
            Vector3 horizontalVelocity = new Vector3(
                rb.linearVelocity.x,
                0f,
                rb.linearVelocity.z
            );

            currentSpeed = horizontalVelocity.magnitude;
        }
        else if (cc != null)
        {
            Vector3 horizontalVelocity = new Vector3(
                cc.velocity.x,
                0f,
                cc.velocity.z
            );

            currentSpeed = horizontalVelocity.magnitude;
        }

        animator.SetFloat("Speed", currentSpeed);

        if (characterState != null)
        {
            // animator.SetBool("IsCarrying", characterState.IsCarrying);
        }
    }
}