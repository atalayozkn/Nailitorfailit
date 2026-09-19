using PlayerScripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_InputField))]
public class ChatInputGuard : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    private bool wasTyping;
    private bool initialized;

    void Awake()
    {
        if (inputField == null) inputField = GetComponent<TMP_InputField>();
    }

    void OnDisable()
    {
        if (wasTyping)
        {
            Apply(false);
            wasTyping = false;
        }
    }

    void Update()
    {
        if (inputField == null) return;

        bool typing = inputField.isFocused;

        if (initialized && typing == wasTyping) return;

        initialized = true;
        wasTyping = typing;

        Apply(typing);
    }

    private void Apply(bool typing)
    {
        PlayerMovement movement = FindLocalPlayerMovement();

        if (movement == null) return;

        SetAction(movement.move, !typing);
        SetAction(movement.jump, !typing);
        SetAction(movement.sprint, !typing);

        Debug.Log($"[CHAT] Player input {(typing ? "DISABLED (typing)" : "enabled")}.");
    }

    private static void SetAction(InputActionReference reference, bool enabled)
    {
        if (reference == null || reference.action == null) return;

        if (enabled)
        {
            if (!reference.action.enabled) reference.action.Enable();
        }
        else
        {
            if (reference.action.enabled) reference.action.Disable();
        }
    }

    private static PlayerMovement FindLocalPlayerMovement()
    {
        foreach (PlayerNetwork_MP net in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            if (net == null || !net.isOwned) continue;

            return net.GetComponentInChildren<PlayerMovement>(true);
        }

        return null;
    }
}
