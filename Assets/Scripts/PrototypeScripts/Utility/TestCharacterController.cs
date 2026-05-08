using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class TestCharacterController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
            return;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            input.y += 1f;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1f;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1f;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1f;

        input = input.normalized;

        Vector3 moveDirection = new Vector3(input.x, 0f, input.y);

        controller.Move(moveDirection * moveSpeed * Time.deltaTime);
    }
}