using UnityEditor;
using UnityEngine;
using System.IO;

namespace ArminPrime
{
    public class BiomeGeneratorWindow : EditorWindow
    {
        private string biomeName = "New Biome";
        private Material biomeMaterial;
        private Color biomeColor = Color.white;

        private string saveDirectory = "Assets/HexGen/Biomes";

        [MenuItem("Tools/HexGen/Generate New Biome")]
        public static void ShowWindow()
        {
            GetWindow<BiomeGeneratorWindow>("Biome Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Biome Generator", EditorStyles.boldLabel);

            // Biome Name Input
            GUILayout.Label("Biome Name:");
            biomeName = EditorGUILayout.TextField(biomeName);

            // Material Picker
            GUILayout.Label("Biome Material:");
            biomeMaterial = (Material)EditorGUILayout.ObjectField(biomeMaterial, typeof(Material), false);

            // Color Picker
            GUILayout.Label("Biome Color:");
            biomeColor = EditorGUILayout.ColorField(biomeColor);

            GUILayout.Space(10);

            // Generate Button
            if (GUILayout.Button("Generate Biome"))
            {
                CreateBiomeAsset();
            }
        }

        private void CreateBiomeAsset()
        {
            if (string.IsNullOrEmpty(biomeName))
            {
                Debug.LogError("Biome name cannot be empty!");
                return;
            }

            // Ensure Save Directory Exists
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }

            // Create Biome Scriptable Object
            BiomeData newBiome = ScriptableObject.CreateInstance<BiomeData>();
            newBiome.biomeName = biomeName;
            newBiome.biomeMaterial = biomeMaterial;
            newBiome.biomeColor = biomeColor;

            // Generate Asset Path
            string assetPath = $"{saveDirectory}/{biomeName}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            // Save the Biome Asset
            AssetDatabase.CreateAsset(newBiome, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Biome '{biomeName}' created at {assetPath}");
        }
    }
}

