using System.Collections.Generic;
using PlayerScripts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private ChatMessageRow rowPrefab;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

    [Header("Behaviour")]
    [SerializeField] private int maxRows = 100;
    [SerializeField] private bool escapeReleasesFocus = true;

    private readonly List<ChatMessageRow> rows = new List<ChatMessageRow>();

    private bool subscribed;
    private bool scrollPending;

    void OnEnable()
    {
        Subscribe();

        if (sendButton != null) sendButton.onClick.AddListener(SendCurrent);
        if (inputField != null) inputField.onSubmit.AddListener(OnSubmit);

        RebuildFromHistory();
    }

    void OnDisable()
    {
        Unsubscribe();

        if (sendButton != null) sendButton.onClick.RemoveListener(SendCurrent);
        if (inputField != null) inputField.onSubmit.RemoveListener(OnSubmit);
    }

    void Subscribe()
    {
        if (subscribed) return;
        ChatNetwork.OnMessage += Append;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed) return;
        ChatNetwork.OnMessage -= Append;
        subscribed = false;
    }

    void Update()
    {
        if (!isOpen) return;

        lastEscapeHandledAt = Time.unscaledTime;

        if (inputField != null && !inputField.isFocused)
        {
            inputField.ActivateInputField();
            inputField.Select();
        }

        if (cachedMovement != null) cachedMovement.SetActivity(false);
        SetPlayerInput(false);

        if (!escapeReleasesFocus || Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            InteractionDebug.Log("[CHAT] Escape pressed - closing the chat.");
            Close();
        }
    }

    void LateUpdate()
    {
        if (!scrollPending) return;
        scrollPending = false;

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    void OnSubmit(string value) => SendCurrent();

    private bool isOpen;

    private PlayerMovement cachedMovement;

    private static float lastEscapeHandledAt = -1f;

    public static ChatUI Active { get; private set; }

    public static bool IsAnyOpen => Active != null && Active.isOpen;

    public static bool EscapeHandledRecently => Time.unscaledTime - lastEscapeHandledAt < 0.2f;

    public bool IsOpen => isOpen;

    public bool IsTyping => isOpen || (inputField != null && inputField.isFocused);

    public void Open()
    {
        for (Transform node = transform; node != null; node = node.parent)
        {
            if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
        }

        if (inputField != null)
        {
            inputField.ActivateInputField();
            inputField.Select();
        }

        isOpen = true;
        Active = this;
        cachedMovement = LocalMovement();

        if (cachedMovement != null) cachedMovement.SetActivity(false);

        SetPlayerInput(false);
        SetCursorLocked(false);
    }

    public void Close()
    {
        if (!isOpen && (inputField == null || !inputField.isFocused)) return;

        isOpen = false;
        lastEscapeHandledAt = Time.unscaledTime;

        if (Active == this) Active = null;

        if (inputField != null) inputField.DeactivateInputField();

        if (cachedMovement != null) cachedMovement.SetActivity(true);

        cachedMovement = null;
        SetPlayerInput(true);
        SetCursorLocked(true);

        InteractionDebug.Log("[CHAT] chat closed, player input restored.");
    }

    private static void SetCursorLocked(bool locked)
    {
        PlayerStateMachine stateMachine = LocalStateMachine();

        if (stateMachine != null)
        {
            if (locked) stateMachine.CloseCursor();
            else stateMachine.OpenCursor();

            return;
        }

        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private static void SetPlayerInput(bool enabled)
    {
        PlayerMovement movement = LocalMovement();
        if (movement == null) return;

        SetAction(movement.move, enabled);
        SetAction(movement.jump, enabled);
        SetAction(movement.sprint, enabled);
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

    private static PlayerMovement LocalMovement()
    {
        foreach (PlayerNetwork_MP net in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            if (net == null || !net.isOwned) continue;
            return net.GetComponentInChildren<PlayerMovement>(true);
        }

        return null;
    }

    private static PlayerStateMachine LocalStateMachine()
    {
        foreach (PlayerNetwork_MP net in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            if (net == null || !net.isOwned) continue;
            return net.GetComponentInChildren<PlayerStateMachine>(true);
        }

        return null;
    }

    void SendCurrent()
    {
        if (inputField == null || ChatNetwork.Instance == null) return;

        if (ChatNetwork.Instance.Send(inputField.text))
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }
    }

    void RebuildFromHistory()
    {
        ClearRows();

        if (ChatNetwork.Instance == null)
        {
            Debug.LogWarning("[CHAT] ChatNetwork.Instance is missing - the chat panel will stay empty. " +
                             "Add the ChatNetwork component to the LobbyNetworkManager GameObject.");
            return;
        }

        IReadOnlyList<ChatEntry> history = ChatNetwork.Instance.History;

        int start = Mathf.Max(0, history.Count - maxRows);

        for (int i = start; i < history.Count; i++)
            AddRow(history[i].sender, history[i].text, false);

        if (history.Count > 0) RequestScrollToBottom();
    }

    void Append(string sender, string text) => AddRow(sender, text, true);

    void AddRow(string sender, string text, bool scrollToBottom)
    {
        if (rowPrefab == null)
        {
            Debug.LogError("[CHAT] Row Prefab is empty. Assign Assets/Prefabs_MP/Chat/ChatMessageRow.prefab " +
                           "to the 'Row Prefab' field on ChatUI.");
            return;
        }

        if (content == null)
        {
            Debug.LogError("[CHAT] Content is empty. Assign Scroll View > Viewport > Content " +
                           "to the Content field on ChatUI.");
            return;
        }

        ChatMessageRow row = Instantiate(rowPrefab, content);
        row.Set(sender, text);
        rows.Add(row);

        while (rows.Count > maxRows)
        {
            ChatMessageRow oldest = rows[0];
            rows.RemoveAt(0);

            if (oldest != null) Destroy(oldest.gameObject);
        }

        if (scrollToBottom) RequestScrollToBottom();
    }

    void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null) Destroy(rows[i].gameObject);

        rows.Clear();
    }

    void RequestScrollToBottom() => scrollPending = true;
}
