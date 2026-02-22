using UnityEngine;
using UnityEditor;

public class SimpleMapPlacer : EditorWindow
{
    GameObject prefab;
    bool placing = false;

    float gridSize = 5f;

    [MenuItem("Tools/Map Placer")]
    public static void ShowWindow()
    {
        GetWindow<SimpleMapPlacer>("Map Placer");
    }

    void OnGUI()
    {
        GUILayout.Label("Grid Object Placer", EditorStyles.boldLabel);

        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);

        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);

        placing = GUILayout.Toggle(placing, "Placement Active");

        if (GUILayout.Button("Stop"))
            placing = false;
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!placing || prefab == null) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 pos = hit.point;

            // GRID SNAP
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
            pos.y = 0f;

            Handles.color = Color.green;
            Handles.DrawWireCube(pos, Vector3.one * gridSize);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                Undo.RegisterCreatedObjectUndo(obj, "Place Object");

                obj.transform.position = pos;

                e.Use();
            }
        }
    }
}
