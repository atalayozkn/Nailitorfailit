using Unity.Cinemachine;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private GamePhase currentPhase;
    private CinemachineCamera inGameCamera;
    private CinemachineCamera inMenuCamera;

    // Instanced
    // Does not destroy OnLoad (1 per game)
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ChangeToMenuPhase();
        OnSceneLoad();

    }
    public void OnSceneLoad()
    {
        if (currentPhase == GamePhase.Menu)
        {
            GameObject inGameCameraObject = GameObject.FindGameObjectWithTag("InGame");
            if (inGameCameraObject != null) inGameCamera = inGameCameraObject.GetComponent<CinemachineCamera>();
            GameObject inMenuCameraObject = GameObject.FindGameObjectWithTag("InMenu");
            if (inMenuCameraObject != null) inMenuCamera = inMenuCameraObject.GetComponent<CinemachineCamera>();
        }
        else if (currentPhase == GamePhase.InGame)
        {
            // Currently empty
        }
        else
        {
            // Currently empty
        }
    }
    // Start from Menu Phase
    #region MENU PHASE
    public void ChangeToMenuPhase()
    {
        if (currentPhase == GamePhase.Menu) return; 
        currentPhase = GamePhase.Menu;
    }

    //Called By Button
    public void ActivateCharacterSelection()
    {
        //Close Menu
        //Open Character Selection Tab
        //Wait for selection
    }

    //Called By Button
    public void StartGame(int index) //index here is the selected character element
    {
        //Alter the prefab of player depending on index
        //Instantiate prefab on the scene
        ChangeToGamePhase();
    }

    #endregion

    #region GAME PHASE
    public void ChangeToGamePhase()
    {
        if (currentPhase == GamePhase.Menu)
        {
            //Close Menu UI
            //Enable InGame UI
            //Shift Camera
            //Reward Starting Money
        }
        else if (currentPhase == GamePhase.InLevel)
        {
            //Load MenuScene
            //Close UI Elements
            //Switch to InGame View
        }

        currentPhase = GamePhase.InGame;

    }

    #endregion

    #region LEVEL PHASE
    public void ChangeToLevelPhase(int index)
    {
        //Darken Screen
        //Switch Scene
        //Load clicked Level Index
    }

    #endregion

    //If player moving between Level - InGame Phases. We should store the player transform.position.

    #region In-Level

    public void CompleteLevel(bool condition)
    {
        //true means level successfully completed
        //false means level is failed
        //Currently we will not do anything
    }

    #endregion

    #region UTILITY

    public GamePhase GetCurrentPhase()
    {
        return currentPhase;
    }

    #endregion
}