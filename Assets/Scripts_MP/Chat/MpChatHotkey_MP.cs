using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class MpChatHotkey_MP : MonoBehaviour
{
    [Tooltip("Open the chat with these keys while in a match.")]
    [SerializeField] private bool openOnC = true;
    [SerializeField] private bool openOnT = true;
    [SerializeField] private bool openOnEnter = false;

    [Tooltip("Log open/close decisions.")]
    [SerializeField] private bool logActions = true;

    private ChatUI cachedChat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpChatHotkey_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpChatHotkey_MP").AddComponent<MpChatHotkey_MP>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (NetworkClient.localPlayer == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        ChatUI chat = FindChat();
        if (chat == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (chat.IsOpen || chat.IsTyping) chat.Close();
            return;
        }

        bool open = (openOnC && keyboard.cKey.wasPressedThisFrame)
                    || (openOnT && keyboard.tKey.wasPressedThisFrame)
                    || (openOnEnter && (keyboard.enterKey.wasPressedThisFrame
                                        || keyboard.numpadEnterKey.wasPressedThisFrame));

        if (!open) return;

        if (chat.IsOpen || chat.IsTyping)
        {
            chat.Close();
            return;
        }

        chat.Open();

        if (logActions) InteractionDebug.Log("[CHAT] chat opened from the MP hotkey.");
    }

    private ChatUI FindChat()
    {
        if (cachedChat != null) return cachedChat;

        foreach (ChatUI chat in FindObjectsByType<ChatUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (chat == null) continue;

            PlayerScripts.PlayerNetwork_MP owner = chat.GetComponentInParent<PlayerScripts.PlayerNetwork_MP>(true);
            if (owner != null && !owner.isOwned) continue;

            cachedChat = chat;
            return cachedChat;
        }

        return null;
    }
}
