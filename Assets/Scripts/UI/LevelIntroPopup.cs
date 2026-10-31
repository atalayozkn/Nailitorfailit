using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Shows the level's intro pop-up the first time the level is played; H reopens it any time.
[RequireComponent(typeof(InfoPopup))]
public class LevelIntroPopup : MonoBehaviour
{
    [SerializeField] private InfoPopupData introData;
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private float startDelay = 0.5f;

    private InfoPopup popup;

    private void Awake()
    {
        popup = GetComponent<InfoPopup>();
        // Only levels can reopen their pop-up with H, so only they show the hint.
        popup.SetHint("Press H to read this again.");
    }
    private IEnumerator Start()
    {
        if (!showOnStart || introData == null || SaveSystem.HasSeenPopup(introData.PopupId)) yield break;

        // Real time: the level may already be paused by something else.
        yield return new WaitForSecondsRealtime(startDelay);
        Open();
    }
    private void Update()
    {
        if (Keyboard.current == null || popup.IsOpen) return;
        if (GamePauseManager.Instance != null && GamePauseManager.Instance.IsPaused) return;
        if (Keyboard.current.hKey.wasPressedThisFrame) Open();
    }

    // Also usable from a UI button (e.g. a "How to play" entry in the pause menu).
    public void Open()
    {
        if (introData == null) return;
        popup.Show(introData, () => SaveSystem.MarkPopupSeen(introData.PopupId));
    }
}
