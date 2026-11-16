using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class MpChatHotkey_MP : MonoBehaviour
{
    [Tooltip("Open the chat with these keys while in a match. C is the crouch key, so only T opens it.")]
    [SerializeField] private bool openOnC = false;
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

        // While typing, T/Enter are part of the message text: the hotkey must not close the
        // chat mid-word (that locked the cursor and dropped everything the player had typed).
        if (chat.IsTyping) return;

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

            // The match chat sits in the package's HUD, a SIBLING of the player body (PlayerNetwork_MP), so a parent
            // search finds no owner and every package's chat looked like "ours": the first one in the scene was
            // opened, and on a client that is the host's package - opening it switched on the host's camera and HUD.
            // The package root's NetworkIdentity tells whose chat it is; the lobby chat has none.
            Mirror.NetworkIdentity package = chat.GetComponentInParent<Mirror.NetworkIdentity>(true);
            if (package != null && !package.isOwned) continue;

            // The disabled authored singleplayer package has no identity either: only the lobby chat may be that.
            if (package == null && Mirror.NetworkClient.localPlayer != null &&
                Mirror.NetworkClient.localPlayer.GetComponent<LobbyPlayer>() == null) continue;

            cachedChat = chat;
            return cachedChat;
        }

        return null;
    }
}
