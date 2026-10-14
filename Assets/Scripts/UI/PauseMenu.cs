using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference pauseButton;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private PlayerStateMachine stateMachine;

    private void Awake()
    {
        if (stateMachine == null) stateMachine = FindFirstObjectByType<PlayerStateMachine>();
    }
    private void OnEnable()
    {
        if (pauseButton == null || pauseButton.action == null) return;

        pauseButton.action.Enable();
        pauseButton.action.performed += OnPausePressed;
    }
    private void OnDisable()
    {
        if (pauseButton != null && pauseButton.action != null)
        {
            pauseButton.action.performed -= OnPausePressed;
            pauseButton.action.Disable();
        }

        if (GamePauseManager.Instance != null && GamePauseManager.Instance.IsPaused)
        {
            GamePauseManager.Instance.SetPause(false);
        }
    }
    private void Start()
    {
        pauseMenu.SetActive(false);
    }

    private void OnPausePressed(InputAction.CallbackContext context)
    {
        SetPauseState(true);
    }

    private void SetPauseState(bool pause)
    {
        if (GamePauseManager.Instance == null) return;
        if (GamePauseManager.Instance.IsPaused == pause) return;

        GamePauseManager.Instance.SetPause(pause);
        pauseMenu.SetActive(pause);

        if (stateMachine == null)
        {
            Debug.LogWarning("PauseMenu: PlayerStateMachine reference is missing.", this);
            return;
        }

        if (pause) stateMachine.OpenCursor();
        else stateMachine.CloseCursor();
    }

    public void Resume()
    {
        SetPauseState(false);
    }
}