using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace ArminPrime
{
    public class HexWorldGeneratorWindow : EditorWindow
    {
        private int columns = 10;
        private int rows = 10;
        private float hexSize = 1f;
        private HexGridManager hexGridManager;
        private List<BiomeData> biomes = new List<BiomeData>();

        public static HexWorldGeneratorWindow Instance { get; private set; }

        [MenuItem("Tools/HexGen/Generate World")]
        public static void ShowWindow()
        {
            HexWorldGeneratorWindow window = GetWindow<HexWorldGeneratorWindow>("Hex World Generator");
            Instance = window;
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnGUI()
        {
            GUILayout.Label("Hex World Generator", EditorStyles.boldLabel);

            columns = EditorGUILayout.IntField("Columns:", columns);
            rows = EditorGUILayout.IntField("Rows:", rows);
            hexSize = EditorGUILayout.FloatField("Hex Size:", hexSize);

            GUILayout.Space(10);
            GUILayout.Label("Biomes", EditorStyles.boldLabel);

            for (int i = 0; i < biomes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                BiomeData selected = (BiomeData)EditorGUILayout.ObjectField(biomes[i], typeof(BiomeData), false);

                if (!biomes.Contains(selected) || selected == biomes[i])
                {
                    biomes[i] = selected;
                }
                else
                {
                    Debug.LogWarning("This biome is already selected!");
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    biomes.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Biome"))
            {
                AddNewBiome();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Confirm & Open Canvas"))
            {
                OpenCanvas();
            }
        }

        private void AddNewBiome()
        {
            BiomeData defaultBiome = null;

            if (!biomes.Contains(defaultBiome))
            {
                biomes.Add(null);
            }
            else
            {
                Debug.LogWarning("Default biome already added or not found.");
            }
        }

        private void OpenCanvas()
        {
            // Check for any null biome slots
            if (biomes.Contains(null))
            {
                Debug.LogWarning("You have to select a biome for all biome slots before opening the canvas.");
                return;
            }

            if (!hexGridManager)
            {
                GameObject hexGridObj = GameObject.Find("GeneratedHexWorld");
                if (!hexGridObj)
                {
                    hexGridObj = new GameObject("GeneratedHexWorld");
                }
                hexGridManager = hexGridObj.GetComponent<HexGridManager>() ?? hexGridObj.AddComponent<HexGridManager>();
            }

            hexGridManager.SetGridSettings(columns, rows, hexSize);
            hexGridManager.SetBiomes(biomes.ToArray());

            // Only open the canvas if all checks are valid
            HexCanvasWindow.ShowCanvas(hexGridManager);
            Close();
        }
    }
}
