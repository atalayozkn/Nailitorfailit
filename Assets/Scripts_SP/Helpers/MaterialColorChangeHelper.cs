using UnityEngine;

public class MaterialColorChangeHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer targetRenderer;

    [Header("Settings")]
    [SerializeField] private Color targetColor;

    private Material material;
    private Color initialColor;

    private void Awake()
    {
        material = targetRenderer.material;
        initialColor = material.color;
    }
    public void ChangeToColor()
    {
        material.color = targetColor;
    }
    public void ReverseToInitialColor()
    {
        material.color = initialColor;
    }
}
