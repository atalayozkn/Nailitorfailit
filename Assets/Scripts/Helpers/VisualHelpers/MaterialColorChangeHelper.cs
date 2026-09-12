using UnityEngine;

public class MaterialColorChangeHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private SkinnedMeshRenderer skinnedRenderer;
    [SerializeField] private bool isSkinnedMesh = false;
    [SerializeField] private string targetParameter = "_BaseColor";

    [Header("Settings")]
    [SerializeField] private Color targetColor;

    private Material material;
    private Color initialColor;

    private void Awake()
    {
        material = isSkinnedMesh ? skinnedRenderer.material : targetRenderer.material;

        if (!material.HasProperty(targetParameter))
        {
            Debug.LogError($"Material {material.name} does not have a color property called '{targetParameter}'.", this);
            return;
        }

        initialColor = material.GetColor(targetParameter);
    }

    public void ChangeToColor()
    {
        if (!material.HasProperty(targetParameter)) return;

        material.SetColor(targetParameter, targetColor);
    }

    public void ReverseToInitialColor()
    {
        if (!material.HasProperty(targetParameter)) return;

        material.SetColor(targetParameter, initialColor);
    }
}