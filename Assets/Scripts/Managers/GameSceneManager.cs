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

    private IEnumerator LoadSceneRoutine(int index)
    {
        // Fade Out
        // Disable input
        // Loading screen

        AsyncOperation operation = SceneManager.LoadSceneAsync(scenes[index]);

        while (!operation.isDone) yield return null;

        activeScene = scenes[index];

        // Tell every persistent manager that the scene is ready.
        GameManager.Instance.OnSceneLoaded();

        // Fade In
        // Enable input
    }
}