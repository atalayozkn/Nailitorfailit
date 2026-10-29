using PlayerScripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_InputField))]
public class ChatInputGuard : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    [Header("Kisayollar")]
    [Tooltip("Bu tusa basinca chat yazma alani acilir.")]
    [SerializeField] private bool useCHotkey = true;

    [Tooltip("Yazarken ESC ile chati kapat.")]
    [SerializeField] private bool escapeClosesChat = true;

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

        if (!typing && useCHotkey && ChatKeyPressed())
        {
            OpenChat();
            return;
        }

        if (typing && escapeClosesChat && EscapePressed())
        {
            inputField.DeactivateInputField();
            return;
        }

        if (initialized && typing == wasTyping) return;

        initialized = true;
        wasTyping = typing;

        Apply(typing);
    }

    private static bool ChatKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (UnityEngine.Input.GetKeyDown(KeyCode.C)) return true;
#endif
        return false;
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
        return false;
    }

    private void OpenChat()
    {
        for (Transform node = transform; node != null; node = node.parent)
        {
            if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
        }

        inputField.ActivateInputField();
        inputField.Select();

        Debug.Log("[CHAT] 'C' ile chat acildi.");
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
