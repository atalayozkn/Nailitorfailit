using UnityEngine;
using GameData.UI.GameState;
using System;

namespace GameData.Manager.GameManager
{
    public class GameManager : MonoBehaviour
    {
        #region Fields
        public static GameManager Instance { get; private set; }
        public EGameState gameState;
        public event Action<EGameState> OnGameStateChanged;
        #endregion
        #region Unity Methods
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
        #endregion
        #region Methods
        public void SetGameState(EGameState newState)
        {
            this.gameState = newState;
            
            OnGameStateChanged?.Invoke(newState);

            Debug.Log($"Oyun Durumu Değişti: {newState}");
        }
        #endregion
    }

}
