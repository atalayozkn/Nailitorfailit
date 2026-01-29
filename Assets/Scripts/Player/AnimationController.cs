using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class AnimationController : NetworkBehaviour
{
    private Animator animator;
    private ICharacterState characterState; // Interface for IsCarrying
    private Rigidbody rb; // Reference to physics body
    private CharacterController cc; // Reference if you use CharacterController instead

    void Start()
    {
        animator = GetComponent<Animator>();

        // Find the "State Provider" (Your PlayerCarry script)
        characterState = GetComponent<ICharacterState>();
        if (characterState == null) characterState = GetComponentInParent<ICharacterState>();

        // Find the Physics Component (Check both self and parent)
        rb = GetComponentInParent<Rigidbody>();
        cc = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        if (!IsOwner) return;
        // 1. Calculate Actual Speed
        float currentSpeed = 0f;

        if (rb != null)
        {
            // Get speed, but ignore "Up/Down" (Jumping shouldn't trigger Run)
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            currentSpeed = horizontalVelocity.magnitude;
        }
        else if (cc != null)
        {
            Vector3 horizontalVelocity = new Vector3(cc.velocity.x, 0, cc.velocity.z);
            currentSpeed = horizontalVelocity.magnitude;
        }

        // 2. Send Speed to Animator
        animator.SetFloat("Speed", currentSpeed);

        // 3. Send Carrying State
        if (characterState != null)
        {
            //animator.SetBool("IsCarrying", characterState.IsCarrying);
        }
    }
}