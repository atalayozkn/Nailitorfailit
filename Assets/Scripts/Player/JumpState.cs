using UnityEngine;

public class JumpState : ICharacterState
{
    private Animator _animator;

    public JumpState(Animator animator)
    {
        _animator = animator;
    }

    public void Enter()
    {
        _animator.SetBool("IsGrounded", false);
        _animator.SetTrigger("Jump");
    }

    public void Tick() { }

    public void Exit()
    {
        _animator.SetBool("IsGrounded", true);
    }
}