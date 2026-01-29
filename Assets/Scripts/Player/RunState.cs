using System;
using UnityEngine;

public class RunState : ICharacterState
{
    private Animator _animator;
    private Func<float> _getSpeed;

    public RunState(Animator animator, Func<float> getSpeed)
    {
        _animator = animator;
        _getSpeed = getSpeed;
    }

    public void Enter() { }

    public void Tick()
    {
        _animator.SetFloat("Speed", _getSpeed());
    }

    public void Exit() { }
}