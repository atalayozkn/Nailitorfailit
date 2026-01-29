using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace ArminPrime
{
    public class LoadHexMapWindow : EditorWindow
    {
        private HexCanvasWindow hexCanvasWindow;
        private string saveDirectory = "Assets/HexGen/Saves";
        private List<string> availableMaps = new List<string>();
        private string selectedMap = null;

        public static void ShowWindow(HexCanvasWindow parent)
        {
            LoadHexMapWindow window = GetWindow<LoadHexMapWindow>("Load Hex Map");
            window.hexCanvasWindow = parent;
            window.LoadAvailableMaps();
        }

        private void LoadAvailableMaps()
        {
            availableMaps.Clear();
            string folderPath = $"{saveDirectory}/{hexCanvasWindow.hexGridManager.width}x{hexCanvasWindow.hexGridManager.height}";

            if (Directory.Exists(folderPath))
            {
                availableMaps.AddRange(Directory.GetFiles(folderPath, "*.json"));
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Select a map to load:", EditorStyles.boldLabel);

            if (availableMaps.Count == 0)
            {
                GUILayout.Label("No saved maps found.");
            }
            else
            {
                foreach (string map in availableMaps)
                {
                    if (GUILayout.Button(Path.GetFileNameWithoutExtension(map)))
                    {
                        selectedMap = map;
                    }
                }
            }

            GUILayout.Space(10);
            if (selectedMap != null && GUILayout.Button("Load Selected Map"))
            {
                hexCanvasWindow.LoadHexMap(selectedMap);
                Close();
            }
        }
    }
}

