using Interactions;
using ItemScript;
using UnityEngine;

namespace GameData
{
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Game/Station Recipe")]
    public class ProcessingRecipe : ScriptableObject
    {
        [Header("Input")]
        public MaterialType inputMaterial;

        [Header("Process")]
        public float workDuration = 3.0f;
        [Tooltip("Does this recipe need a specific tool? (e.g. Saw needed for wood)")]
        public Tools requiredTool = Tools.None;

        [Header("Output")]
        public GameObject outputPrefab; // The item to spawn (Prefab with NetworkObject)
        public int outputCount = 1;     // How many to spawn
    }

}