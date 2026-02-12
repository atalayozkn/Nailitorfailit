using UnityEngine;
using GameData.UI.GameState;
using GameData.Manager.GameManager;
using Unity.VisualScripting;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace GameData.UI.UIManager
{
    public class UIManager : MonoBehaviour
    {
        #region Fields
        [Header("Panels")]
        [SerializeField] private GameObject m_MainMenuPanel;
        [SerializeField] private GameObject m_GamePanel;
        [SerializeField] private GameObject m_PauseMenuPanel;
        [SerializeField] private GameObject m_EndMenuPanel;
        #endregion

        #region Unity Methods
        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }
        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }
        private void Start()
        {
            ToMainMenu();
        }
        #endregion
        #region Methods
        private void HandleGameStateChanged(EGameState gameState)
        {
            m_MainMenuPanel.SetActive(gameState == EGameState.MAINMENU);
            m_GamePanel.SetActive(gameState == EGameState.GAME);
            m_PauseMenuPanel.SetActive(gameState == EGameState.PAUSEMENU);
            m_EndMenuPanel.SetActive(gameState == EGameState.ENDMENU);
        }
        public void ToMainMenu()
        {
            if (GameManager.Instance != null)
            {
                Debug.Log($"ToMainMenu():");
                GameManager.Instance.SetGameState(EGameState.MAINMENU);
            }
        }
        public void ToGame()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(EGameState.GAME);
            }
        }
        public void ToPauseMenu()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(EGameState.PAUSEMENU);
            }
        }
        public void ToEndMenu()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(EGameState.ENDMENU);
            }
        }
        #endregion
    }

}
