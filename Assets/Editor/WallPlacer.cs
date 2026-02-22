using UnityEngine;
using UnityEditor;

public class WallPlacer : EditorWindow
{
    GameObject wallPrefab;
    float grid = 5f;
    bool active;

    float rotation;
    GameObject ghost;

    Material previewMat;

    [MenuItem("Tools/Wall Placer")]
    static void Open()
    {
        GetWindow<WallPlacer>("Wall Placer");
    }

    void OnGUI()
    {
        wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall Prefab", wallPrefab, typeof(GameObject), false);
        grid = EditorGUILayout.FloatField("Grid Size", grid);
        active = GUILayout.Toggle(active, "Placement Active");

        GUILayout.Label("Q/E Rotate");
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

        if (ghost != null)
            DestroyImmediate(ghost);

        DestroyImmediate(previewMat);
    }

    void SceneGUI(SceneView view)
    {
        if (!active || wallPrefab == null) return;

        Event e = Event.current;

        if (e.type == EventType.KeyDown)
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
            p.y = 0.05f; // biraz yukarý

            if (Mathf.Abs(rotation % 180) < 1)
                p.z += grid / 2f;
            else
                p.x += grid / 2f;

            if (ghost == null)
            {
                ghost = Instantiate(wallPrefab);
                ghost.hideFlags = HideFlags.HideAndDontSave;

                foreach (var c in ghost.GetComponentsInChildren<Collider>())
                    DestroyImmediate(c);

                foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = previewMat;
            }

            ghost.transform.position = p;
            ghost.transform.rotation = Quaternion.Euler(0, rotation, 0);

            Bounds b = ghost.GetComponentInChildren<Renderer>().bounds;

            Handles.color = Color.green;
            Handles.DrawWireCube(b.center, b.size);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                GameObject o = PrefabUtility.InstantiatePrefab(wallPrefab) as GameObject;
                Undo.RegisterCreatedObjectUndo(o, "Place Wall");

                o.transform.position = p;
                o.transform.rotation = Quaternion.Euler(0, rotation, 0);

                e.Use();
            }
        }
    }
}
