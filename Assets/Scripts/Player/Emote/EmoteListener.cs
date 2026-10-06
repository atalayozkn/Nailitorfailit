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