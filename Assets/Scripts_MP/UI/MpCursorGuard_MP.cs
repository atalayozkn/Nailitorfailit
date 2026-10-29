using Mirror;
using UnityEngine;

public class MpCursorGuard_MP : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpCursorGuard_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpCursorGuard_MP").AddComponent<MpCursorGuard_MP>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        bool inGame = NetworkClient.active && NetworkClient.localPlayer != null;

        if (inGame) return;

        if (Cursor.lockState == CursorLockMode.None && Cursor.visible) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
