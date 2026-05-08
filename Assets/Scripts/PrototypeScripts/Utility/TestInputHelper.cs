using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class TestInputHelper : MonoBehaviour
{
    [SerializeField] private Key testInput;

    [SerializeField] private UnityEvent onGetKeyDownEvent;

    private void Update()
    {
        if (Keyboard.current[testInput].wasPressedThisFrame)
        {
            onGetKeyDownEvent?.Invoke();
        }
    }
}