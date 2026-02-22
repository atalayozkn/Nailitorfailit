using UnityEngine;
using UnityEditor;

public class LevelBuilderTool : EditorWindow
{
    enum BuildMode { Ground, Wall }

    BuildMode mode;

    GameObject groundPrefab;
    GameObject wallPrefab;

    float grid = 5f;
    bool active;

    float rotation;
    GameObject ghost;
    Material previewMat;

    [MenuItem("Tools/Level Builder")]
    static void Open()
    {
        GetWindow<LevelBuilderTool>("Level Builder");
    }

    void OnGUI()
    {
        GUILayout.Label("Level Builder", EditorStyles.boldLabel);

        mode = (BuildMode)EditorGUILayout.EnumPopup("Mode", mode);

        if (mode == BuildMode.Ground)
            groundPrefab = (GameObject)EditorGUILayout.ObjectField("Ground Prefab", groundPrefab, typeof(GameObject), false);

        if (mode == BuildMode.Wall)
            wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall Prefab", wallPrefab, typeof(GameObject), false);

        grid = EditorGUILayout.FloatField("Grid Size", grid);
        active = GUILayout.Toggle(active, "Placement Active");

        if (mode == BuildMode.Wall)
            GUILayout.Label("Q / E Rotate");
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += SceneGUI;

        previewMat = new Material(Shader.Find("Unlit/Color"));
        previewMat.color = new Color(0, 1, 0, 0.4f);
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= SceneGUI;

        if (ghost) DestroyImmediate(ghost);
        DestroyImmediate(previewMat);
    }

    void SceneGUI(SceneView view)
    {
        if (!active) return;

        GameObject prefab = mode == BuildMode.Ground ? groundPrefab : wallPrefab;
        if (!prefab) return;

        Event e = Event.current;

        if (mode == BuildMode.Wall && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Q) rotation -= 90;
            if (e.keyCode == KeyCode.E) rotation += 90;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 p = hit.point;

            p.x = Mathf.Round(p.x / grid) * grid;
            p.z = Mathf.Round(p.z / grid) * grid;
            p.y = 0.05f;

            if (mode == BuildMode.Wall)
            {
                if (Mathf.Abs(rotation % 180) < 1)
                    p.z += grid / 2f;
                else
                    p.x += grid / 2f;
            }

            if (!ghost)
            {
                ghost = Instantiate(prefab);
                ghost.hideFlags = HideFlags.HideAndDontSave;

                foreach (Collider c in ghost.GetComponentsInChildren<Collider>())
                    DestroyImmediate(c);

                foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = previewMat;
            }

            ghost.transform.position = p;
            ghost.transform.rotation = Quaternion.Euler(0, rotation, 0);

            Bounds b = ghost.GetComponentInChildren<Renderer>().bounds;
            Handles.DrawWireCube(b.center, b.size);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                GameObject o = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                Undo.RegisterCreatedObjectUndo(o, "Place");

                o.transform.position = p;
                o.transform.rotation = Quaternion.Euler(0, rotation, 0);

                e.Use();
            }
        }
    }
}
