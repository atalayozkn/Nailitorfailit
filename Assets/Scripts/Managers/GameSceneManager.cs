using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }
    [SerializeField] private string[] scenes;
    private string activeScene;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        activeScene = SceneManager.GetActiveScene().name;
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    public void LoadMenu()
    {
        StartCoroutine(LoadSceneRoutine(0));
    }
    public void LoadLevel(int index)
    {
        StartCoroutine(LoadSceneRoutine(index));
    }
    public string GetActiveScene()
    {
        return activeScene;
    }
    // Index of the active scene inside the scenes array (also used as level index), -1 if not listed.
    public int GetActiveSceneIndex()
    {
        return System.Array.IndexOf(scenes, SceneManager.GetActiveScene().name);
    }
    // Playable levels are every scene after the menu (index 0).
    public int GetLevelCount()
    {
        return scenes.Length - 1;
    }

    private IEnumerator LoadSceneRoutine(int index)
    {
        // Fade Out
        // Disable input
        // Loading screen

        AsyncOperation operation = SceneManager.LoadSceneAsync(scenes[index]);

        while (!operation.isDone) yield return null;

        activeScene = scenes[index];

        // Leaving a level from the pause menu keeps timeScale at 0 (GamePauseManager may be destroyed
        // before PauseMenu.OnDisable can unpause). Every freshly loaded scene must start unpaused,
        // otherwise scaled timers like the level start camera/input Invokes never fire.
        Time.timeScale = 1f;

        // Tell every persistent manager that the scene is ready.
        GameManager.Instance.OnSceneLoaded();

        // Fade In
        // Enable input
    }
}