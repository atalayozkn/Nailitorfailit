using System.IO;
using UnityEngine;

// Headless entry point: Unity.exe -batchmode -projectPath <project> -executeMethod WarmStyleBatch.Run -quit
// Applies the warm look to the tutorial and Levels 1-5 and saves before/after captures to Captures~/Levels.
public static class WarmStyleBatch
{
    public static void Run()
    {
        string dir = Path.GetFullPath("Assets/StyleTest_Warm/Captures~/Levels") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(dir);

        foreach (var path in StyleTestBuilder.WarmScenes)
        {
            string log = StyleTestBuilder.ApplyToScene(path, name => Shoot(dir + name + "_before.png", Vector3.zero));
            Debug.Log("[WarmStyle] " + log);

            string scene = Path.GetFileNameWithoutExtension(path);
            Shoot(dir + scene + "_after.png", Vector3.zero);
            var house = GameObject.Find("HouseCenter");
            var room = Object.FindFirstObjectByType<RoomController>(FindObjectsInactive.Include);
            var target = house != null ? house.transform.position : room != null ? room.transform.position : Vector3.zero;
            StyleTestBuilder.CaptureAt(dir + scene + "_overview.png", target, new Vector3(0f, 26f, -20f), 1600, 900);
        }
        File.Delete(dir + "_warmup.png");
        Debug.Log("[WarmStyle] done");
    }

    // The first frames compile shader variants, so render a few times before saving.
    static void Shoot(string file, Vector3 offset)
    {
        string warm = Path.Combine(Path.GetDirectoryName(file), "_warmup.png");
        for (int i = 0; i < 2; i++) StyleTestBuilder.Capture(warm, offset, 480, 240);
        StyleTestBuilder.Capture(file, offset, 1600, 800);
    }
}
