using UnityEngine;

public class MaterialColorChangeHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private SkinnedMeshRenderer skinnedRenderer;
    [SerializeField] private bool isSkinnedMesh = false;

    [Header("Settings")]
    [SerializeField] private Color targetColor;

    private Material material;
    private Color initialColor;

    private void Awake()
    {
        if (!isSkinnedMesh)
        {
            material = targetRenderer.material;
            initialColor = material.color;
            return;
        }

        material = skinnedRenderer.material;
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
