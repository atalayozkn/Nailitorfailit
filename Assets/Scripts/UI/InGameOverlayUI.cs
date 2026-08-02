using UnityEngine;

public class InGameOverlayUI : MonoBehaviour
{
    [SerializeField] private GameObject overlayUIParent;
    public void SetActivity(bool condition)
    {
        overlayUIParent.SetActive(condition);
    }
}
