using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the rain mutator to the 5 levels, switched off. Tick "Mutator Enabled" on a level's RainMutatorController to use it.
// Tools > NailIt > Add Rain Mutator To All Levels
public static class RainMutatorSetup
{
    private const string MaterialPath = "Assets/Art/Materials/RainPuddle.mat";
    private const string PolePrefabPath = "Assets/Prefabs/Elektrik Direkleri";

    private static readonly string[] Levels =
    {
        "Assets/Scenes/Level_01.unity",
        "Assets/Scenes/Level_02.unity",
        "Assets/Scenes/Level_03.unity",
        "Assets/Scenes/Level_04.unity",
        "Assets/Scenes/Level_05.unity",
    };

    [MenuItem("Tools/NailIt/Add Rain Mutator To All Levels")]
    private static void AddToAllLevels()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Material material = GetOrCreateMaterial();

        foreach (string path in Levels)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            RainController_Prototype rain = Object.FindFirstObjectByType<RainController_Prototype>(FindObjectsInactive.Include);
            GameObject host = rain != null ? rain.gameObject : new GameObject("Rain_Mutator");

            RainMutatorController mutator = host.GetComponent<RainMutatorController>();
            if (mutator == null) mutator = host.AddComponent<RainMutatorController>();

            SerializedObject serialized = new SerializedObject(mutator);
            serialized.FindProperty("rainController").objectReferenceValue = rain;
            serialized.FindProperty("puddleMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"RainMutatorSetup: Added to {path} (rain controller found: {rain != null}).");
        }
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");

        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
