using UnityEngine;

namespace ArminPrime
{
    [CreateAssetMenu(fileName = "NewBiome", menuName = "HexMap/BiomeData")]
    public class BiomeData : ScriptableObject
    {
        public string biomeName;
        public Material biomeMaterial;
        public Color biomeColor;
    }
}

 