using System.IO;
using UnityEditor;
using UnityEngine;

// Headless entry point: Unity.exe -batchmode -projectPath <project> -executeMethod StyleTestBatch.Run -quit
public static class StyleTestBatch
{
    public static void Run()
    {
        string dir = Path.GetFullPath("Assets/StyleTest_Warm/Captures~") + Path.DirectorySeparatorChar;
        string log = StyleTestBuilder.Rebuild(true);
        Debug.Log("[StyleTest] " + log);

        // The first frames compile shader variants, so render a few times before saving.
        for (int i = 0; i < 3; i++) StyleTestBuilder.Capture(dir + "_warmup.png", Vector3.zero, 640, 320);
        StyleTestBuilder.Capture(dir + "after_gameplay_camera.png", Vector3.zero, 1600, 800);
        StyleTestBuilder.Capture(dir + "after_low_angle.png", new Vector3(0f, 7.5f, -13f), 1600, 800);
        File.Delete(dir + "_warmup.png");
        Debug.Log("[StyleTest] captures saved");
    }
}
