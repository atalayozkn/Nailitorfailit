using UnityEditor;
using UnityEngine;

// Inspector for NailIt/SimpleToon: the Outline toggle also turns the outline pass on/off,
// so materials without an outline skip that draw completely.
public class SimpleToonGUI : ShaderGUI
{
    public const string OutlinePass = "SRPDefaultUnlit";

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();
        base.OnGUI(materialEditor, properties);
        if (!EditorGUI.EndChangeCheck()) return;

        foreach (var target in materialEditor.targets)
        {
            if (target is Material material) SyncOutlinePass(material);
        }
    }

    public static void SyncOutlinePass(Material material)
    {
        bool outline = material.HasProperty("_Outline") && material.GetFloat("_Outline") > 0.5f;
        material.SetShaderPassEnabled(OutlinePass, outline);
    }
}
