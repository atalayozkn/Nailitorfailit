using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Radial emote picker: while the emote key is held the cursor is freed and placed at the wheel's centre,
// the tab in the mouse's direction is highlighted, and releasing the key plays it.
// A small dead zone in the middle selects nothing, so releasing there cancels.
public class EmoteWheel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private List<EmoteTab> emoteTabs;

    [Header("Selection")]
    [Tooltip("Dead zone radius as a share of the distance from the centre to the tabs.")]
    [SerializeField, Range(0.05f, 0.9f)] private float deadZone = 0.35f;
    [Tooltip("Optional text in the hub showing the highlighted emote.")]
    [SerializeField] private TMPro.TMP_Text centerLabel;
    [SerializeField] private string idleText = "EMOTE";

    private EmoteTab currentTab;
    private bool isActive;
    private Vector2 center;
    private readonly List<Vector2> tabDirections = new();
    private float deadZonePixels;

    private void OnEnable()
    {
        isActive = false;
        currentTab = null;
        canvasRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (isActive) Close(false);
    }

    private void Update()
    {
        if (!isActive || Mouse.current == null) return;

        Vector2 offset = Mouse.current.position.ReadValue() - center;
        EmoteTab next = null;
        if (offset.magnitude >= deadZonePixels)
        {
            float best = float.MinValue;
            Vector2 dir = offset.normalized;
            for (int i = 0; i < emoteTabs.Count; i++)
            {
                float dot = Vector2.Dot(dir, tabDirections[i]);
                if (dot > best) { best = dot; next = emoteTabs[i]; }
            }
            // Pointing at a locked slot selects nothing.
            if (next != null && next.IsLocked) next = null;
        }
        Select(next);
    }

    private void Select(EmoteTab tab)
    {
        if (tab == currentTab) return;
        if (currentTab != null) currentTab.OnHoverOff();
        currentTab = tab;
        if (currentTab != null) currentTab.OnHoverOn();
        if (centerLabel != null) centerLabel.text = currentTab != null ? currentTab.Label : idleText;
    }

    public void SetActivity(bool condition)
    {
        if (isActive == condition) return;
        if (condition) Open();
        else Close(true);
    }

    // Closes without playing anything (e.g. the game got paused while the wheel was open).
    public void Cancel()
    {
        if (isActive) Close(false);
    }

    private void Open()
    {
        // The wheel object itself must be on for Update to run; only canvasRoot hides the visuals.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        isActive = true;
        canvasRoot.SetActive(true);
        CursorManager.Show(this);
        // Multiplayer: CursorManager is singleplayer-only (no-op there); unlock directly,
        // same path ChatUI uses, so the wheel tabs can actually be hovered.
        if (NetworkModeHelper.IsNetworked)
        {
            PlayerStateMachine stateMachine = GetComponentInParent<PlayerStateMachine>();
            if (stateMachine != null) stateMachine.OpenCursor();
        }
        Canvas.ForceUpdateCanvases();
        MeasureWheel();
        Mouse.current?.WarpCursorPosition(center);
        currentTab = null;
        foreach (var tab in emoteTabs) tab.OnHoverOff();
        if (centerLabel != null) centerLabel.text = idleText;
    }

    private void Close(bool execute)
    {
        isActive = false;
        if (currentTab != null)
        {
            if (execute) currentTab.Execute();
            currentTab.OnHoverOff();
        }
        currentTab = null;
        canvasRoot.SetActive(false);
        CursorManager.Release(this);
        // Multiplayer: hand the cursor back (skip when the pause menu already took it).
        if (NetworkModeHelper.IsNetworked)
        {
            bool paused = GamePauseManager.Instance != null && GamePauseManager.Instance.IsPaused;
            PlayerStateMachine stateMachine = GetComponentInParent<PlayerStateMachine>();
            if (!paused && stateMachine != null) stateMachine.CloseCursor();
        }
    }

    // Screen-space centre of the tabs and the direction to each, measured on open so any resolution works.
    private void MeasureWheel()
    {
        tabDirections.Clear();
        var points = new List<Vector2>();
        foreach (var tab in emoteTabs)
        {
            var rt = tab.Anchor;
            var canvas = tab.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            points.Add(RectTransformUtility.WorldToScreenPoint(cam, rt.position));
        }

        center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (points.Count > 1)
        {
            center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Count;
        }

        float minDistance = float.MaxValue;
        foreach (var p in points)
        {
            Vector2 d = p - center;
            minDistance = Mathf.Min(minDistance, d.magnitude);
            tabDirections.Add(d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.up);
        }
        deadZonePixels = (minDistance < float.MaxValue ? minDistance : 100f) * deadZone;
    }
}
