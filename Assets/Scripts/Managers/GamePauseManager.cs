using UnityEngine;

public class GamePauseManager : MonoBehaviour
{
    public static GamePauseManager Instance { get; private set; }
    public bool IsPaused { get; private set; }

    private PlayerMovement pMovementHandler;
    private PlayerInteractionHandler pInteractionHandler;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CacheReferences();
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    public void SetPause(bool pause)
    {
        IsPaused = pause;

        if (pause)
        {
            Time.timeScale = 0f;
            pMovementHandler.SetActivity(false);
            pInteractionHandler.SetActivity(false);
        }
        else
        {
            Time.timeScale = 1.0f;
            pMovementHandler.SetActivity(true);
            pInteractionHandler.SetActivity(true);
        }
    }
    private void CacheReferences()
    {
        pMovementHandler = FindFirstObjectByType<PlayerMovement>();
        pInteractionHandler = FindFirstObjectByType<PlayerInteractionHandler>();
    }
}