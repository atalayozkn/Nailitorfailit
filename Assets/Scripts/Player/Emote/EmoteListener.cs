using UnityEngine;
using UnityEngine.InputSystem;

public class EmoteListener : MonoBehaviour
{
    [SerializeField] private EmoteWheel wheel;
    [SerializeField] private InputActionReference emoteAction;

    private bool isActive;

    private void OnEnable()
    {
        isActive = true;
    }

    private void Update()
    {
        if (!isActive) return;

        bool paused = GamePauseManager.Instance != null && GamePauseManager.Instance.IsPaused;
        if (paused)
        {
            wheel.Cancel();
            return;
        }

        if (emoteAction.action.WasPressedThisFrame())
        {
            wheel.SetActivity(true);
        }
        else if (emoteAction.action.WasReleasedThisFrame())
        {
            wheel.SetActivity(false);
        }
    }

    private void SetActivity(bool condition)
    {
        isActive = condition;
    }
}