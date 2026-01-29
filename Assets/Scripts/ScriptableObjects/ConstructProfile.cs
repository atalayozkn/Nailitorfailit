using Interactions;
using UnityEngine;

namespace GameData
{
    [CreateAssetMenu(fileName = "NewConstruct", menuName = "Game/Construct Profile")]
    public class ConstructProfile : ScriptableObject
    {
        public ConstructType constructType;
        public MaterialType requiredMaterial;
        public bool requiresToolToFinalize = false;
        public Tools finishingTool = Tools.Hammer;
    }
}