using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference pauseButton;
    [SerializeField] private GameObject pauseMenu;

    private void OnEnable()
    {
        pauseButton.action.Enable();
        pauseButton.action.performed += TogglePause;
    }

    private void OnDisable()
    {
        pauseButton.action.performed -= TogglePause;
        pauseButton.action.Disable();
    }

    private void Start()
    {
        pauseMenu.SetActive(false);
    }

    private void Update()
    {
        if (pauseButton.action != null && pauseButton.action.WasPressedThisFrame())
        {
            pauseMenu.SetActive(true);
        }
    }

    private void TogglePause(InputAction.CallbackContext context)
    {
        SetPauseState(!GamePauseManager.Instance.IsPaused);
    }

    private void SetPauseState(bool pause)
    {
        if (pause)
        {
            GamePauseManager.Instance.SetPause(true);
            pauseMenu.SetActive(true);
        }
        else
        {
            GamePauseManager.Instance.SetPause(false);
            pauseMenu.SetActive(false);
        }
    }
    public void Resume()
    {
        SetPauseState(false);
    }
}