using UnityEngine;

public class HighlightHelper : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private bool isMeshRenderer = true;

    private Material mat;
    private void Awake()
    {
        if (isMeshRenderer) mat = meshRenderer.material;
        else mat = skinnedMeshRenderer.material;
    }
    private void Start()
    {
        if (mat != null)
        {
            ReverseHighlight();
        }
    }
    public void HighlightMaterial()
    {
        mat.SetFloat("_HighColor_Power", 1f);
    }
    public void ReverseHighlight()
    {
        mat.SetFloat("_HighColor_Power", 0f);
    }
}
