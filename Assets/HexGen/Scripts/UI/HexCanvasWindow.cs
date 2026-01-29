using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace ArminPrime
{
    public class HexCanvasWindow : EditorWindow
    {
        public HexGridManager hexGridManager;
        private BiomeData selectedBiome;
        private Dictionary<Vector2Int, BiomeData> biomeAssignments = new Dictionary<Vector2Int, BiomeData>();
        private Dictionary<Vector2Int, int> tileHeights = new Dictionary<Vector2Int, int>();

        private int hexSize = 40;
        private string saveDirectory = "Assets/HexGen/Saves";
        private string mapName = "NewMap";

        private int selectedTab = 0; // 0 = Biome Painting, 1 = Height Painting
        private int selectedHeight = 1; // Default height value

        public static void ShowCanvas(HexGridManager manager)
        {
            HexCanvasWindow window = GetWindow<HexCanvasWindow>("Hex Editor");
            window.hexGridManager = manager;
        }

        private void OnGUI()
        {
            GUILayout.Label("HexGen Editor", EditorStyles.boldLabel);

            selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "Paint Biomes", "Paint Heights" });

            if (selectedTab == 0)
            {
                DrawBiomeSelectionUI();
            }
            else
            {
                DrawHeightPaintingUI();
            }
            GUILayout.Space(30);
            DrawHexCanvas();

            GUILayout.Space(30);

            GUILayout.Label("Save / Load Map", EditorStyles.boldLabel);
            mapName = EditorGUILayout.TextField("Map Name:", mapName);

            if (GUILayout.Button("Save Map")) SaveHexMap(mapName);
            if (GUILayout.Button("Load Map")) LoadHexMapWindow.ShowWindow(this);

            if (GUILayout.Button("Generate World"))
            {
                ApplyBiomesToGrid();
                hexGridManager.GenerateWorld();
                hexGridManager.gameObject.name = "Generated_Map";
                DestroyImmediate(hexGridManager);
                Close();
            }
        }

        private void DrawBiomeSelectionUI()
        {
            GUILayout.Label("Select Biome", EditorStyles.boldLabel);
            float buttonWidth = position.width / 2f;
            float colorBoxSize = 20f;

            foreach (BiomeData biome in hexGridManager.biomes)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = (biome == selectedBiome) ? new Color(0.5f, 0.5f, 0.5f, 1f) : GUI.backgroundColor;

                if (GUILayout.Button(biome.biomeName, GUILayout.Width(buttonWidth)))
                {
                    selectedBiome = biome;
                }

                GUI.backgroundColor = Color.white;

                Rect colorRect = GUILayoutUtility.GetRect(colorBoxSize, colorBoxSize);
                Vector2 circleCenter = new Vector2(colorRect.x + colorBoxSize / 2, colorRect.y + colorBoxSize / 2);
                Handles.color = biome.biomeColor;
                Handles.DrawSolidDisc(circleCenter, Vector3.forward, colorBoxSize / 2);

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawHeightPaintingUI()
        {
            GUILayout.Label("Select Height", EditorStyles.boldLabel);
            selectedHeight = EditorGUILayout.IntSlider("Height:", selectedHeight, 0, 10);
        }

        private void DrawHexCanvas()
        {
            float availableWidth = position.width - 20;
            float availableHeight = position.height - 200;
            hexSize = Mathf.FloorToInt(Mathf.Min(availableWidth / hexGridManager.width, availableHeight / hexGridManager.height));
            hexSize = Mathf.Clamp(hexSize, 5, 40);

            Event e = Event.current;

            for (int y = 0; y < hexGridManager.height; y++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < hexGridManager.width; x++)
                {
                    Vector2Int currentHex = new Vector2Int(x, hexGridManager.height - 1 - y);
                    Color originalColor = GUI.color;
                    Rect hexRect = GUILayoutUtility.GetRect(hexSize, hexSize);
                    float yOffset = (x % 2 == 1) ? -hexSize * 0.5f : 0;
                    hexRect.y += yOffset;

                    // **Determine Biome Color**
                    Color baseColor = biomeAssignments.ContainsKey(currentHex) ? biomeAssignments[currentHex].biomeColor : new Color(0.7f, 0.7f, 0.7f, 1f);

                    // **Apply Height Tinting**
                    int heightLevel = tileHeights.ContainsKey(currentHex) ? tileHeights[currentHex] : 0; // Default height 1
                                                                                                         // Normalize height (increase contrast so small heights don’t look too dark)
                                                                                                         // Define a color gradient for height levels (you can adjust these)
                    Color[] heightColors = new Color[]
                    {
                    new Color(0.6f, 0.4f, 0.2f), // Level 1 (Brownish, lower altitude)
                    new Color(0.7f, 0.5f, 0.3f), // Level 2
                    new Color(0.8f, 0.6f, 0.4f), // Level 3
                    new Color(0.5f, 0.7f, 0.4f), // Level 4 (Greenish, mid-altitude)
                    new Color(0.3f, 0.6f, 0.5f), // Level 5
                    new Color(0.2f, 0.5f, 0.7f), // Level 6 (Bluish, high altitude)
                    new Color(0.4f, 0.4f, 0.8f), // Level 7
                    new Color(0.5f, 0.3f, 0.7f), // Level 8 (Purple tint, mountain peaks)
                    new Color(0.6f, 0.2f, 0.6f), // Level 9
                    new Color(0.8f, 0.1f, 0.5f)  // Level 10 (Pinkish, very high peaks)
                    };

                    // Ensure height is within range
                    int heightIndex = Mathf.Clamp(heightLevel - 1, 0, heightColors.Length - 1);

                    // Get the tint color from our height gradient
                    Color heightTint = heightColors[heightIndex];

                    // Blend biome color with height tint
                    float blendFactor = 0.5f; // Adjust this to control how strong the tinting effect is
                    GUI.color = Color.Lerp(baseColor, heightTint, blendFactor);
                    EditorGUI.DrawRect(hexRect, GUI.color);
                    GUI.color = originalColor;

                    // **Draw Grid Borders**
                    Handles.color = Color.black;
                    Handles.DrawLine(new Vector3(hexRect.x, hexRect.y), new Vector3(hexRect.xMax, hexRect.y)); // Top
                    Handles.DrawLine(new Vector3(hexRect.x, hexRect.yMax), new Vector3(hexRect.xMax, hexRect.yMax)); // Bottom
                    Handles.DrawLine(new Vector3(hexRect.x, hexRect.y), new Vector3(hexRect.x, hexRect.yMax)); // Left
                    Handles.DrawLine(new Vector3(hexRect.xMax, hexRect.y), new Vector3(hexRect.xMax, hexRect.yMax)); // Right

                    // **Display Height Level on Tile (Optional)**
                    GUI.color = Color.black;
                    GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Clamp(hexSize / 3, 8, 12) };
                    EditorGUI.LabelField(hexRect, heightLevel.ToString(), style);
                    GUI.color = originalColor;

                    // **Painting Logic**
                    if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                    {
                        if (hexRect.Contains(e.mousePosition))
                        {
                            if (selectedTab == 0 && selectedBiome != null)
                            {
                                biomeAssignments[currentHex] = selectedBiome;
                            }
                            else if (selectedTab == 1)
                            {
                                tileHeights[currentHex] = selectedHeight;
                            }
                            Repaint();
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void SaveHexMap(string mapName)
        {
            HexMapData mapData = new HexMapData
            {
                width = hexGridManager.width,
                height = hexGridManager.height
            };

            foreach (var entry in biomeAssignments)
            {
                HexCellData cellData = new HexCellData
                {
                    x = entry.Key.x,
                    y = entry.Key.y,
                    biomeName = entry.Value.biomeName,
                    height = tileHeights.ContainsKey(entry.Key) ? tileHeights[entry.Key] : 0 // Default height if missing
                };
                mapData.cells.Add(cellData);
            }

            string json = JsonUtility.ToJson(mapData, true);
            File.WriteAllText(GetSavePath(mapName), json);
            Debug.Log($"Hex Map Saved: {mapName}");
        }

        public void LoadHexMap(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning("Map file not found!");
                return;
            }

            string json = File.ReadAllText(filePath);
            HexMapData mapData = JsonUtility.FromJson<HexMapData>(json);

            hexGridManager.width = mapData.width;
            hexGridManager.height = mapData.height;
            biomeAssignments.Clear();
            tileHeights.Clear();

            // Apply the loaded biomes and heights
            List<BiomeData> loadedBiomes = new List<BiomeData>();

            foreach (HexCellData cellData in mapData.cells)
            {
                BiomeData biome = FindBiomeByName(cellData.biomeName);
                if (biome != null)
                {
                    biomeAssignments[new Vector2Int(cellData.x, cellData.y)] = biome;
                    tileHeights[new Vector2Int(cellData.x, cellData.y)] = cellData.height;

                    if (!loadedBiomes.Contains(biome))
                    {
                        loadedBiomes.Add(biome);
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not assign biome '{cellData.biomeName}' at ({cellData.x}, {cellData.y})");
                }
            }

            // Update the biomes in HexGridManager
            hexGridManager.SetBiomes(loadedBiomes.ToArray());

            // **Force UI Repaint After Loading**
            Repaint();
        }
        private string GetSavePath(string mapName)
        {
            string folderPath = $"{saveDirectory}/{hexGridManager.width}x{hexGridManager.height}";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return $"{folderPath}/{mapName}.json";
        }
        private BiomeData FindBiomeByName(string biomeName)
        {
            string biomeFolderPath = "Assets/HexGen/Biomes";
            string[] guids = AssetDatabase.FindAssets("t:BiomeData", new[] { biomeFolderPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BiomeData biome = AssetDatabase.LoadAssetAtPath<BiomeData>(path);

                if (biome != null && biome.biomeName == biomeName)
                {
                    return biome;
                }
            }

            Debug.LogWarning($"Biome '{biomeName}' not found in {biomeFolderPath}!");
            return null;
        }

        private void ApplyBiomesToGrid()
        {
            foreach (var entry in biomeAssignments)
            {
                hexGridManager.SetBiome(entry.Key.x, entry.Key.y, entry.Value);
            }
            foreach (var entry in tileHeights)
            {
                hexGridManager.SetTileHeight(entry.Key.x, entry.Key.y, entry.Value);
            }
        }
    }
}

