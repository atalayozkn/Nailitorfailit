using UnityEngine;

public class IdleState : ICharacterState
{
    private Animator _animator;

    public IdleState(Animator animator)
    {
        _animator = animator;
    }

    public void Enter()
    {
        _animator.SetFloat("Speed", 0f);
    }

    public void Tick() { }

    public void Exit() { }
}