using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    //Instanced is not Destroyed on Load

    [SerializeField] private string[] scenes;
    //scenes[0] will be Main Menu
    //others will be levels

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
        LoadScene(0);
    }

    public void LoadLevel(int index)
    {
        LoadScene(index);
    }

    private void LoadScene(int index)
    {
        if (index < 0 || index >= scenes.Length)
        {
            Debug.LogError($"Scene index {index} is out of range.");
            return;
        }

        SceneManager.LoadScene(scenes[index]);
        activeScene = scenes[index];
    }
    public string GetActiveScene()
    {
        return activeScene;
    }
}