using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Automated Play mode check: puts the player behind the hut and confirms the hut dithers
// instead of disappearing. Run: Unity.exe -projectPath <project> -executeMethod StyleTestPlayCheck.Run
[InitializeOnLoad]
public static class StyleTestPlayCheck
{
    const string Flag = "StyleTestPlayCheck";
    static int frames;

    static StyleTestPlayCheck()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false))
            {
                frames = 0;
                EditorApplication.update += Tick;
            }
        };
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/StyleTest_Warm/Level_01_WarmStyle.unity");
        SessionState.SetBool(Flag, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        frames++;
        var player = GameObject.Find("Player_Default");
        if (frames == 120 && player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.transform.position = new Vector3(-19.5f, player.transform.position.y, 15.5f);
            if (cc) cc.enabled = true;
        }
        if (frames < 240) return;

        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int dithered = renderers.Count(r => r.sharedMaterials.Any(m => m && m.shader.name == "Hidden/OcclusionDither/WarmStylized"));
        int hidden = renderers.Count(r => !r.enabled && r.gameObject.activeInHierarchy && !r.name.StartsWith("sokak"));
        Debug.Log($"[StyleTest] play check: player={(player ? player.transform.position.ToString() : "missing")} ditheredRenderers={dithered} disabledRenderers={hidden}");
        string dir = Path.GetFullPath("Assets/StyleTest_Warm/Captures~") + Path.DirectorySeparatorChar;
        StyleTestBuilder.Capture(dir + "play_dither_check.png", Vector3.zero, 1600, 800);
        EditorApplication.Exit(0);
    }
}
